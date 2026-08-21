using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Activation interface for Salesforce triggers driven by polling rather than a live event
/// stream (CDC/Pub-Sub, §6 — not built in this pass) or an inbound webhook (not usable by a
/// third-party trigger at all, per docs/dev-notes.md §0a: Core's webhook endpoint is hardcoded to its own
/// concrete <c>WebhookTrigger</c> type). Mirrors Core's own <see cref="IScheduledTrigger"/> —
/// same shape, evaluated by <see cref="SalesforcePollingBackgroundJob"/> instead of Core's
/// scheduled-trigger job.
/// </summary>
public interface ISalesforcePollingTrigger
{
    /// <summary>
    /// Executes one poll for a single automation and returns the trigger events to dispatch
    /// (may be empty) plus the checkpoint to persist afterwards.
    /// </summary>
    /// <param name="context">
    /// The resolved connection, client, previous checkpoint, and this automation's trigger
    /// settings for this poll.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<SalesforcePollResult> PollAsync(SalesforcePollingContext context, CancellationToken cancellationToken);
}
