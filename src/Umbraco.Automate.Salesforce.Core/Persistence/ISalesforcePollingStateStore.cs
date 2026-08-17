namespace Umbraco.Automate.Salesforce.Persistence;

/// <summary>
/// Persists per-automation polling checkpoints so Salesforce polling triggers survive restarts
/// (mirrors Core's own <c>IScheduledTriggerStateStore</c> for the same reason).
/// </summary>
public interface ISalesforcePollingStateStore
{
    /// <summary>
    /// Gets the checkpoint for the given automation, or a fresh (never-polled) state if none exists.
    /// </summary>
    Task<SalesforcePollingState> GetStateAsync(Guid automationId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the checkpoint for the given automation.
    /// </summary>
    Task SaveStateAsync(Guid automationId, SalesforcePollingState state, CancellationToken cancellationToken);
}
