using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Salesforce.Persistence;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// The outcome of one <see cref="ISalesforcePollingTrigger.PollAsync"/> call: the trigger events
/// to dispatch (possibly empty) and the checkpoint to persist once they're dispatched.
/// </summary>
public sealed record SalesforcePollResult(IReadOnlyList<TriggerEvent> Events, SalesforcePollingState NextState);
