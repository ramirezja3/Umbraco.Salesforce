using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Connection;
using Umbraco.Automate.Salesforce.Persistence;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Everything an <see cref="ISalesforcePollingTrigger"/> needs to execute one poll.
/// </summary>
public sealed class SalesforcePollingContext
{
    /// <summary>Gets the automation being polled for.</summary>
    public required Guid AutomationId { get; init; }

    /// <summary>Gets the trigger alias, for building idempotency keys.</summary>
    public required string TriggerAlias { get; init; }

    /// <summary>Gets the automation's resolved trigger settings, or <c>null</c> if unconfigured.</summary>
    public object? Settings { get; init; }

    /// <summary>Gets the resolved Salesforce connection to poll (see <see cref="SalesforceTriggerConnectionResolver"/>).</summary>
    public required SalesforceConnectionContext Connection { get; init; }

    /// <summary>Gets the client to call the Salesforce REST API with.</summary>
    public required ISalesforceClient Client { get; init; }

    /// <summary>Gets the checkpoint from the previous successful poll.</summary>
    public required SalesforcePollingState PreviousState { get; init; }

    /// <summary>
    /// Gets the timestamp this poll started at — the exclusive upper bound of the query window,
    /// so a record created/modified mid-poll is picked up on the <em>next</em> poll rather than
    /// silently skipped or double-counted.
    /// </summary>
    public required DateTime PollStartedUtc { get; init; }
}
