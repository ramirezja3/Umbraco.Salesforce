using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Persistence;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Fires when a record of a configured Salesforce object is updated after creation. Polling-based
/// — see <see cref="RecordCreatedTrigger"/>'s remarks for why, and for the same "first poll only
/// establishes a baseline" behaviour.
/// </summary>
[Trigger("salesforce.recordUpdated", "Record Updated",
    Description = "Fires when a record of a configured Salesforce object is updated.",
    Group = "CRM",
    Icon = "icon-edit")]
public sealed class RecordUpdatedTrigger
    : TriggerBase<RecordUpdatedTriggerSettings, RecordUpdatedTriggerOutput>, ISalesforcePollingTrigger
{
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordUpdatedTrigger"/> class.
    /// </summary>
    public RecordUpdatedTrigger(TriggerInfrastructure infrastructure, IOptionsMonitor<SalesforceApiOptions> options)
        : base(infrastructure)
    {
        _options = options;
    }

    /// <inheritdoc />
    public async Task<SalesforcePollResult> PollAsync(SalesforcePollingContext context, CancellationToken cancellationToken)
    {
        var settings = context.Settings as RecordUpdatedTriggerSettings;
        if (string.IsNullOrWhiteSpace(settings?.ObjectApiName))
        {
            return new SalesforcePollResult([], context.PreviousState);
        }

        if (context.PreviousState.LastPollUtc is null)
        {
            return new SalesforcePollResult([], new SalesforcePollingState(context.PollStartedUtc, context.PreviousState.Snapshot));
        }

        var extraFields = string.IsNullOrWhiteSpace(settings.Fields) ? "" : $",{settings.Fields}";
        var soql =
            $"SELECT Id,LastModifiedDate{extraFields} FROM {settings.ObjectApiName} " +
            $"WHERE LastModifiedDate > {SalesforceTriggerSupport.FormatSoqlDateTime(context.PreviousState.LastPollUtc.Value)} " +
            $"AND LastModifiedDate <= {SalesforceTriggerSupport.FormatSoqlDateTime(context.PollStartedUtc)} " +
            // Excludes never-touched-since-creation records — those belong to RecordCreatedTrigger.
            "AND LastModifiedDate != CreatedDate " +
            $"ORDER BY LastModifiedDate ASC LIMIT {_options.CurrentValue.MaxQueryRows}";

        var apiVersion = _options.CurrentValue.ApiVersion;
        var result = await context.Client.SendAsync(
            context.Connection, HttpMethod.Get, $"/services/data/{apiVersion}/query?q={Uri.EscapeDataString(soql)}",
            jsonBody: null, cancellationToken);

        if (!result.IsSuccess || result.Json is not { } json)
        {
            return new SalesforcePollResult([], context.PreviousState);
        }

        var events = new List<TriggerEvent>();
        if (json.TryGetProperty("records", out var records))
        {
            foreach (var record in records.EnumerateArray())
            {
                var fields = SalesforceJsonHelpers.ToFieldDictionary(record);
                var recordId = (string)fields["Id"]!;
                var lastModified = DateTime.Parse((string)fields["LastModifiedDate"]!).ToUniversalTime();

                events.Add(new TriggerEvent<RecordUpdatedTriggerOutput>
                {
                    TriggerAlias = context.TriggerAlias,
                    InitiatorType = TriggerInitiatorType.System,
                    TargetAutomationId = context.AutomationId,
                    // Includes LastModifiedDate so distinct updates to the same record get
                    // distinct keys — unlike Create/Delete, a record can legitimately fire this
                    // trigger many times.
                    IdempotencyKey = $"salesforce.recordUpdated:{context.AutomationId}:{recordId}:{lastModified:O}",
                    Output = new RecordUpdatedTriggerOutput
                    {
                        RecordId = recordId,
                        ObjectApiName = settings.ObjectApiName,
                        LastModifiedDateUtc = lastModified,
                        Fields = fields,
                    },
                });
            }
        }

        return new SalesforcePollResult(events, new SalesforcePollingState(context.PollStartedUtc, context.PreviousState.Snapshot));
    }
}
