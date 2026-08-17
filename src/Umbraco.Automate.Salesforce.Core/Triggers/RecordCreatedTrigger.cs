using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Persistence;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Fires when a new record of a configured Salesforce object is created. Polling-based (see
/// <see cref="ISalesforcePollingTrigger"/>) — CDC/Pub-Sub is the documented preference for
/// near-real-time firing (CLAUDE.md §6) but isn't buildable/testable without a live org; this is
/// the fallback the brief explicitly allows, checked every <c>Umbraco:Automate:Salesforce:Polling:PollInterval</c>.
/// </summary>
/// <remarks>
/// The very first poll for a newly-published automation only establishes a baseline — it does
/// not fire for records that already existed before the automation was published. This avoids a
/// flood of events for pre-existing data the implementer almost certainly doesn't want replayed.
/// </remarks>
[Trigger("salesforce.recordCreated", "Record Created",
    Description = "Fires when a new record of a configured Salesforce object is created.",
    Group = "CRM",
    Icon = "icon-add")]
public sealed class RecordCreatedTrigger
    : TriggerBase<RecordCreatedTriggerSettings, RecordCreatedTriggerOutput>, ISalesforcePollingTrigger
{
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordCreatedTrigger"/> class.
    /// </summary>
    public RecordCreatedTrigger(TriggerInfrastructure infrastructure, IOptionsMonitor<SalesforceApiOptions> options)
        : base(infrastructure)
    {
        _options = options;
    }

    /// <inheritdoc />
    public async Task<SalesforcePollResult> PollAsync(SalesforcePollingContext context, CancellationToken cancellationToken)
    {
        var settings = context.Settings as RecordCreatedTriggerSettings;
        if (string.IsNullOrWhiteSpace(settings?.ObjectApiName))
        {
            return new SalesforcePollResult([], context.PreviousState);
        }

        // First-ever poll: establish a baseline, don't backfill pre-existing records.
        if (context.PreviousState.LastPollUtc is null)
        {
            return new SalesforcePollResult([], new SalesforcePollingState(context.PollStartedUtc, context.PreviousState.Snapshot));
        }

        var extraFields = string.IsNullOrWhiteSpace(settings.Fields) ? "" : $",{settings.Fields}";
        var soql =
            $"SELECT Id,CreatedDate{extraFields} FROM {settings.ObjectApiName} " +
            $"WHERE CreatedDate > {SalesforceTriggerSupport.FormatSoqlDateTime(context.PreviousState.LastPollUtc.Value)} " +
            $"AND CreatedDate <= {SalesforceTriggerSupport.FormatSoqlDateTime(context.PollStartedUtc)} " +
            $"ORDER BY CreatedDate ASC LIMIT {_options.CurrentValue.MaxQueryRows}";

        var apiVersion = _options.CurrentValue.ApiVersion;
        var result = await context.Client.SendAsync(
            context.Connection, HttpMethod.Get, $"/services/data/{apiVersion}/query?q={Uri.EscapeDataString(soql)}",
            jsonBody: null, cancellationToken);

        if (!result.IsSuccess || result.Json is not { } json)
        {
            // Leave LastPollUtc where it was — retry this window on the next poll rather than
            // silently skipping records because of a transient API failure.
            return new SalesforcePollResult([], context.PreviousState);
        }

        var events = new List<TriggerEvent>();
        if (json.TryGetProperty("records", out var records))
        {
            foreach (var record in records.EnumerateArray())
            {
                var fields = SalesforceJsonHelpers.ToFieldDictionary(record);
                var recordId = (string)fields["Id"]!;
                var createdDate = DateTime.Parse((string)fields["CreatedDate"]!).ToUniversalTime();

                events.Add(new TriggerEvent<RecordCreatedTriggerOutput>
                {
                    TriggerAlias = context.TriggerAlias,
                    InitiatorType = TriggerInitiatorType.System,
                    TargetAutomationId = context.AutomationId,
                    IdempotencyKey = $"salesforce.recordCreated:{context.AutomationId}:{recordId}",
                    Output = new RecordCreatedTriggerOutput
                    {
                        RecordId = recordId,
                        ObjectApiName = settings.ObjectApiName,
                        CreatedDateUtc = createdDate,
                        Fields = fields,
                    },
                });
            }
        }

        return new SalesforcePollResult(events, new SalesforcePollingState(context.PollStartedUtc, context.PreviousState.Snapshot));
    }
}
