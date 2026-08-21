using Umbraco.Automate.Salesforce.Api;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Result of <see cref="SalesforceTriggerSupport.SeedSnapshotAsync"/>.
/// </summary>
/// <param name="Succeeded">
/// <c>false</c> if a page request failed partway through — the caller should discard
/// <paramref name="Snapshot"/> and retry the whole seed next poll rather than treat a partial
/// sweep as complete.
/// </param>
/// <param name="Truncated">
/// <c>true</c> if the seed stopped early because it hit the configured row cap rather than
/// draining every page. The caller should log this — an organization with more records than the
/// cap will have some records missing their baseline.
/// </param>
/// <param name="Snapshot">The <c>Id</c> → field-value map collected so far.</param>
/// <param name="Error">The mapped error from the failing request, when <paramref name="Succeeded"/> is <c>false</c>.</param>
internal sealed record SalesforceSeedResult(bool Succeeded, bool Truncated, Dictionary<string, string> Snapshot, SalesforceApiError? Error);
