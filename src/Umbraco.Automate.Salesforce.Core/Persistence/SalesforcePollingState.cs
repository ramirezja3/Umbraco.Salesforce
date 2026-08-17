namespace Umbraco.Automate.Salesforce.Persistence;

/// <summary>
/// A Salesforce polling trigger's checkpoint for one automation: the exclusive upper bound of
/// the last successful poll window, plus an optional recordId → last-known-value snapshot for
/// triggers that need to detect a specific field changing (e.g. Opportunity Stage Changed).
/// </summary>
public sealed record SalesforcePollingState(DateTime? LastPollUtc, IReadOnlyDictionary<string, string> Snapshot)
{
    /// <summary>The state for an automation that has never been polled.</summary>
    public static readonly SalesforcePollingState Initial = new(null, new Dictionary<string, string>());
}
