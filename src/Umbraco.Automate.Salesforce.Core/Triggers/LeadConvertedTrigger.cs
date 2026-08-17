using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Persistence;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Fires when a Lead is converted. Polling-based — see <see cref="RecordCreatedTrigger"/>'s remarks.
/// </summary>
/// <remarks>
/// Conversion is one-way (<c>IsConverted</c> only ever flips false → true), but a converted
/// Lead's <c>LastModifiedDate</c> can advance again later for unrelated reasons (e.g. someone
/// edits its description), which would otherwise re-enter the poll window and fire a second
/// time. The snapshot is used purely as a "have we already fired for this Lead" set to prevent
/// that — there's no old/new value to diff, unlike <see cref="OpportunityStageChangedTrigger"/>.
/// </remarks>
[Trigger("salesforce.leadConverted", "Lead Converted",
    Description = "Fires when a Lead is converted.",
    Group = "CRM",
    Icon = "icon-merge")]
public sealed class LeadConvertedTrigger
    : TriggerBase<object, LeadConvertedTriggerOutput>, ISalesforcePollingTrigger
{
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadConvertedTrigger"/> class.
    /// </summary>
    public LeadConvertedTrigger(TriggerInfrastructure infrastructure, IOptionsMonitor<SalesforceApiOptions> options)
        : base(infrastructure)
    {
        _options = options;
    }

    /// <inheritdoc />
    public async Task<SalesforcePollResult> PollAsync(SalesforcePollingContext context, CancellationToken cancellationToken)
    {
        if (context.PreviousState.LastPollUtc is null)
        {
            return new SalesforcePollResult([], new SalesforcePollingState(context.PollStartedUtc, context.PreviousState.Snapshot));
        }

        var soql =
            "SELECT Id,ConvertedContactId,ConvertedAccountId,ConvertedOpportunityId,LastModifiedDate FROM Lead " +
            "WHERE IsConverted = true " +
            $"AND LastModifiedDate > {SalesforceTriggerSupport.FormatSoqlDateTime(context.PreviousState.LastPollUtc.Value)} " +
            $"AND LastModifiedDate <= {SalesforceTriggerSupport.FormatSoqlDateTime(context.PollStartedUtc)} " +
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
        var seen = new Dictionary<string, string>(context.PreviousState.Snapshot);

        if (json.TryGetProperty("records", out var records))
        {
            foreach (var record in records.EnumerateArray())
            {
                var fields = SalesforceJsonHelpers.ToFieldDictionary(record);
                var leadId = (string)fields["Id"]!;

                if (seen.ContainsKey(leadId))
                {
                    continue;
                }

                seen[leadId] = "1";

                events.Add(new TriggerEvent<LeadConvertedTriggerOutput>
                {
                    TriggerAlias = context.TriggerAlias,
                    InitiatorType = TriggerInitiatorType.System,
                    TargetAutomationId = context.AutomationId,
                    IdempotencyKey = $"salesforce.leadConverted:{context.AutomationId}:{leadId}",
                    Output = new LeadConvertedTriggerOutput
                    {
                        LeadId = leadId,
                        ConvertedContactId = fields.TryGetValue("ConvertedContactId", out var c) ? c as string : null,
                        ConvertedAccountId = fields.TryGetValue("ConvertedAccountId", out var a) ? a as string : null,
                        ConvertedOpportunityId = fields.TryGetValue("ConvertedOpportunityId", out var o) ? o as string : null,
                    },
                });
            }
        }

        return new SalesforcePollResult(events, new SalesforcePollingState(context.PollStartedUtc, seen));
    }
}
