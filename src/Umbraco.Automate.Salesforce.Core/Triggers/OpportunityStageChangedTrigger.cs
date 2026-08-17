using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Persistence;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Fires when an Opportunity's <c>StageName</c> changes, optionally filtered to a specific
/// target stage. Polling-based — see <see cref="RecordCreatedTrigger"/>'s remarks.
/// </summary>
/// <remarks>
/// Unlike the other polling triggers, this can't just skip its first poll — it needs to have
/// <em>observed</em> a record's stage once before it can tell whether a later poll changed it.
/// So every poll queries and updates the per-record stage snapshot; a record only fires once
/// there's a previous snapshot entry to compare against, which means the first time any given
/// Opportunity is observed (on the automation's first poll, or the first time an
/// previously-unseen-because-unchanged Opportunity is touched) never fires — it just seeds the
/// baseline. This is the same "don't backfill" behaviour the other triggers have, arrived at
/// one poll later because detecting a change needs a prior observation to diff against.
/// </remarks>
[Trigger("salesforce.opportunityStageChanged", "Opportunity Stage Changed",
    Description = "Fires when an Opportunity's stage changes, optionally filtered to a target stage.",
    Group = "CRM",
    Icon = "icon-trending")]
public sealed class OpportunityStageChangedTrigger
    : TriggerBase<OpportunityStageChangedTriggerSettings, OpportunityStageChangedTriggerOutput>, ISalesforcePollingTrigger
{
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityStageChangedTrigger"/> class.
    /// </summary>
    public OpportunityStageChangedTrigger(TriggerInfrastructure infrastructure, IOptionsMonitor<SalesforceApiOptions> options)
        : base(infrastructure)
    {
        _options = options;
    }

    /// <inheritdoc />
    public async Task<SalesforcePollResult> PollAsync(SalesforcePollingContext context, CancellationToken cancellationToken)
    {
        var settings = context.Settings as OpportunityStageChangedTriggerSettings;

        // Still nothing to compare against on the very first poll — see remarks — but we do run
        // the query so newly-touched records get seeded into the snapshot from poll #1 onward.
        // A 1-day lookback bounds that first query instead of pulling every Opportunity modified
        // since the dawn of the org; it only affects which records get an initial baseline
        // sooner rather than later, never whether a real change gets missed.
        var lowerBound = context.PreviousState.LastPollUtc ?? context.PollStartedUtc.AddDays(-1);

        var soql =
            "SELECT Id,StageName,Amount,AccountId,OwnerId,LastModifiedDate FROM Opportunity " +
            $"WHERE LastModifiedDate > {SalesforceTriggerSupport.FormatSoqlDateTime(lowerBound)} " +
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
        var snapshot = new Dictionary<string, string>(context.PreviousState.Snapshot);

        if (json.TryGetProperty("records", out var records))
        {
            foreach (var record in records.EnumerateArray())
            {
                var fields = SalesforceJsonHelpers.ToFieldDictionary(record);
                var recordId = (string)fields["Id"]!;
                var newStage = (string)fields["StageName"]!;
                var hasPrevious = snapshot.TryGetValue(recordId, out var previousStage);

                snapshot[recordId] = newStage;

                if (!hasPrevious || previousStage == newStage)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(settings?.TargetStage)
                    && !string.Equals(newStage, settings.TargetStage, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                events.Add(new TriggerEvent<OpportunityStageChangedTriggerOutput>
                {
                    TriggerAlias = context.TriggerAlias,
                    InitiatorType = TriggerInitiatorType.System,
                    TargetAutomationId = context.AutomationId,
                    IdempotencyKey = $"salesforce.opportunityStageChanged:{context.AutomationId}:{recordId}:{fields["LastModifiedDate"]}",
                    Output = new OpportunityStageChangedTriggerOutput
                    {
                        OpportunityId = recordId,
                        PreviousStage = previousStage!,
                        NewStage = newStage,
                        Amount = fields.TryGetValue("Amount", out var amount) ? amount as double? : null,
                        AccountId = fields.TryGetValue("AccountId", out var accountId) ? accountId as string : null,
                        OwnerId = fields.TryGetValue("OwnerId", out var ownerId) ? ownerId as string : null,
                    },
                });
            }
        }

        return new SalesforcePollResult(events, new SalesforcePollingState(context.PollStartedUtc, snapshot));
    }
}
