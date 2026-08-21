using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Persistence;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Fires when an Opportunity's <c>StageName</c> changes, optionally filtered to a specific
/// target stage — e.g. "Closed Won" triggering a case-study publish or a sales-owner notification
/// on the website. Polling-based (see <see cref="ISalesforcePollingTrigger"/>): CDC/Pub-Sub is the
/// documented preference for near-real-time firing (docs/dev-notes.md §6) but isn't buildable/testable
/// without a live organization; this is the fallback the brief explicitly allows, checked every
/// <c>Umbraco:Automate:Salesforce:Polling:PollInterval</c>.
/// </summary>
/// <remarks>
/// Unlike the other polling triggers, this can't just skip its first poll — it needs to have
/// <em>observed</em> a record's stage once before it can tell whether a later poll changed it.
/// So every poll queries and updates the per-record stage snapshot; a record only fires once
/// there's a previous snapshot entry to compare against, which means the first time any given
/// Opportunity is observed never fires — it just seeds the baseline. This is the same
/// "don't backfill" behaviour the other triggers have, arrived at one poll later because
/// detecting a change needs a prior observation to diff against.
/// <para>
/// The automation's very first poll ever (<see cref="Persistence.SalesforcePollingState.LastPollUtc"/>
/// is <c>null</c>) is handled separately, via an unfiltered, paginated seed sweep of every
/// Opportunity's current stage (<see cref="SalesforceTriggerSupport.SeedSnapshotAsync"/>) rather
/// than the normal <c>LastModifiedDate</c>-windowed query below. A previous version of this
/// trigger bounded even that first poll to a 1-day lookback, which meant any Opportunity not
/// otherwise touched in the 24 hours before the automation went live never got a baseline —
/// so when it was later modified, including the exact stage change being watched for, the
/// "no previous value" rule silently swallowed that real event instead of firing it. Seeding
/// unconditionally from every existing record closes that gap: only records created after the
/// automation went live can hit "no previous value" again, which is correct for them.
/// </para>
/// </remarks>
[Trigger("salesforce.opportunityStageChanged", "Opportunity Stage Changed",
    Description = "Fires when an Opportunity's stage changes, optionally filtered to a target stage.",
    Group = "Salesforce",
    Icon = "icon-trending")]
public sealed class OpportunityStageChangedTrigger
    : TriggerBase<OpportunityStageChangedTriggerSettings, OpportunityStageChangedTriggerOutput>, ISalesforcePollingTrigger
{
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;
    private readonly ILogger<OpportunityStageChangedTrigger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityStageChangedTrigger"/> class.
    /// </summary>
    public OpportunityStageChangedTrigger(
        TriggerInfrastructure infrastructure,
        IOptionsMonitor<SalesforceApiOptions> options,
        ILogger<OpportunityStageChangedTrigger> logger)
        : base(infrastructure)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SalesforcePollResult> PollAsync(SalesforcePollingContext context, CancellationToken cancellationToken)
    {
        var settings = context.Settings as OpportunityStageChangedTriggerSettings;

        if (context.PreviousState.LastPollUtc is null)
        {
            return await SeedFirstPollAsync(context, cancellationToken);
        }

        var lowerBound = context.PreviousState.LastPollUtc.Value;

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
            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Salesforce Opportunity Stage Changed trigger poll failed for automation {AutomationId}: {Error}",
                    context.AutomationId, result.Error?.Message);
            }

            return new SalesforcePollResult([], context.PreviousState);
        }

        var events = new List<TriggerEvent>();
        var snapshot = new Dictionary<string, string>(context.PreviousState.Snapshot);
        var recordCount = 0;
        DateTime? lastModifiedWatermark = null;

        if (json.TryGetProperty("records", out var records))
        {
            foreach (var record in records.EnumerateArray())
            {
                recordCount++;
                var fields = SalesforceJsonHelpers.ToFieldDictionary(record);
                var recordId = (string)fields["Id"]!;
                var newStage = (string)fields["StageName"]!;
                lastModifiedWatermark = DateTime.Parse((string)fields["LastModifiedDate"]!).ToUniversalTime();
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
                        Amount = fields.TryGetValue("Amount", out var amount) ? SalesforceJsonHelpers.ToDouble(amount) : null,
                        AccountId = fields.TryGetValue("AccountId", out var accountId) ? accountId as string : null,
                        OwnerId = fields.TryGetValue("OwnerId", out var ownerId) ? ownerId as string : null,
                    },
                });
            }
        }

        var nextPollUtc = SalesforceTriggerSupport.ComputeNextPollWatermark(
            recordCount, _options.CurrentValue.MaxQueryRows, lastModifiedWatermark, context.PollStartedUtc);

        return new SalesforcePollResult(events, new SalesforcePollingState(nextPollUtc, snapshot));
    }

    /// <summary>
    /// Handles the automation's very first poll ever — see the type-level remarks for why this is
    /// a full, unfiltered seed sweep rather than the normal 1-day-lookback incremental query a
    /// previous version of this trigger used.
    /// </summary>
    private async Task<SalesforcePollResult> SeedFirstPollAsync(SalesforcePollingContext context, CancellationToken cancellationToken)
    {
        var seed = await SalesforceTriggerSupport.SeedSnapshotAsync(
            context.Connection,
            context.Client,
            _options.CurrentValue.ApiVersion,
            "Opportunity",
            "StageName",
            _options.CurrentValue.MaxSeedRows,
            cancellationToken);

        if (!seed.Succeeded)
        {
            _logger.LogWarning(
                "Salesforce Opportunity Stage Changed trigger's initial seed failed for automation {AutomationId}: {Error}",
                context.AutomationId, seed.Error?.Message);

            // Leave PreviousState (LastPollUtc still null) untouched so the next poll retries the
            // seed from scratch rather than persisting a partial snapshot as if it were complete.
            return new SalesforcePollResult([], context.PreviousState);
        }

        if (seed.Truncated)
        {
            _logger.LogWarning(
                "Salesforce Opportunity Stage Changed trigger's initial seed for automation {AutomationId} stopped after " +
                "{MaxSeedRows} records (Umbraco:Automate:Salesforce:MaxSeedRows) — this organization has more Opportunities " +
                "than that, so some records won't have a baseline and may miss their first stage change after this cap. " +
                "Increase MaxSeedRows if that matters for this organization.",
                context.AutomationId, _options.CurrentValue.MaxSeedRows);
        }

        // The seed sweep itself never fires events — there is no "previous" stage to diff against
        // for a record observed for the very first time, the same rule the incremental path above
        // applies per-record. Merge into any existing snapshot rather than replace it outright, in
        // case a prior seed attempt partially succeeded before this package's own logic decided to
        // retry (SeedSnapshotAsync always starts from an empty dictionary, so this only matters if
        // a caller ever passes a non-empty PreviousState.Snapshot in here, which today never happens
        // since PreviousState.LastPollUtc is null only on a truly fresh checkpoint).
        var snapshot = new Dictionary<string, string>(context.PreviousState.Snapshot);
        foreach (var (id, stage) in seed.Snapshot)
        {
            snapshot[id] = stage;
        }

        return new SalesforcePollResult([], new SalesforcePollingState(context.PollStartedUtc, snapshot));
    }
}
