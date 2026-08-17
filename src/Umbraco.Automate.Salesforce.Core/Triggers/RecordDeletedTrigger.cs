using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Persistence;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Fires when a record of a configured Salesforce object is deleted. Polling-based, using
/// Salesforce's dedicated Deleted Records endpoint (<c>GET .../sobjects/{type}/deleted/</c>) —
/// deleted records aren't visible to a normal SOQL query, so this is a distinct call shape from
/// the other polling triggers, not a variant of the same SOQL-based query.
/// </summary>
[Trigger("salesforce.recordDeleted", "Record Deleted",
    Description = "Fires when a record of a configured Salesforce object is deleted.",
    Group = "CRM",
    Icon = "icon-trash")]
public sealed class RecordDeletedTrigger
    : TriggerBase<RecordDeletedTriggerSettings, RecordDeletedTriggerOutput>, ISalesforcePollingTrigger
{
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordDeletedTrigger"/> class.
    /// </summary>
    public RecordDeletedTrigger(TriggerInfrastructure infrastructure, IOptionsMonitor<SalesforceApiOptions> options)
        : base(infrastructure)
    {
        _options = options;
    }

    /// <inheritdoc />
    public async Task<SalesforcePollResult> PollAsync(SalesforcePollingContext context, CancellationToken cancellationToken)
    {
        var settings = context.Settings as RecordDeletedTriggerSettings;
        if (string.IsNullOrWhiteSpace(settings?.ObjectApiName))
        {
            return new SalesforcePollResult([], context.PreviousState);
        }

        if (context.PreviousState.LastPollUtc is null)
        {
            return new SalesforcePollResult([], new SalesforcePollingState(context.PollStartedUtc, context.PreviousState.Snapshot));
        }

        var start = Uri.EscapeDataString(SalesforceTriggerSupport.FormatSoqlDateTime(context.PreviousState.LastPollUtc.Value));
        var end = Uri.EscapeDataString(SalesforceTriggerSupport.FormatSoqlDateTime(context.PollStartedUtc));
        var apiVersion = _options.CurrentValue.ApiVersion;
        var path = $"/services/data/{apiVersion}/sobjects/{Uri.EscapeDataString(settings.ObjectApiName)}/deleted/?start={start}&end={end}";

        var result = await context.Client.SendAsync(context.Connection, HttpMethod.Get, path, jsonBody: null, cancellationToken);

        if (!result.IsSuccess || result.Json is not { } json)
        {
            return new SalesforcePollResult([], context.PreviousState);
        }

        var events = new List<TriggerEvent>();
        if (json.TryGetProperty("deletedRecords", out var deletedRecords))
        {
            foreach (var record in deletedRecords.EnumerateArray())
            {
                var recordId = record.GetProperty("id").GetString()!;
                // Not JsonElement.GetDateTime(): Salesforce emits "+0000" (no colon in the UTC
                // offset), which System.Text.Json's strict RFC 3339 parser rejects outright —
                // confirmed against a real org. DateTime.Parse handles it, same as every other
                // trigger's date fields (which go through SalesforceJsonHelpers, string-typed).
                var deletedDate = DateTime.Parse(record.GetProperty("deletedDate").GetString()!,
                    null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);

                events.Add(new TriggerEvent<RecordDeletedTriggerOutput>
                {
                    TriggerAlias = context.TriggerAlias,
                    InitiatorType = TriggerInitiatorType.System,
                    TargetAutomationId = context.AutomationId,
                    IdempotencyKey = $"salesforce.recordDeleted:{context.AutomationId}:{recordId}",
                    Output = new RecordDeletedTriggerOutput
                    {
                        RecordId = recordId,
                        ObjectApiName = settings.ObjectApiName,
                        DeletedDateUtc = deletedDate,
                    },
                });
            }
        }

        return new SalesforcePollResult(events, new SalesforcePollingState(context.PollStartedUtc, context.PreviousState.Snapshot));
    }
}
