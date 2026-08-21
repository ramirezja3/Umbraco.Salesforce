using System.Globalization;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Small shared helpers for polling triggers.
/// </summary>
internal static class SalesforceTriggerSupport
{
    /// <summary>
    /// Formats a UTC <see cref="DateTime"/> as a SOQL datetime literal (unquoted, unlike a SOQL
    /// string literal).
    /// </summary>
    public static string FormatSoqlDateTime(DateTime utc)
        => utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    /// <summary>
    /// Seeds a full current-state snapshot (<c>Id</c> → <paramref name="snapshotFieldApiName"/>)
    /// for every record of <paramref name="objectApiName"/>, paginating through Salesforce's
    /// <c>nextRecordsUrl</c> until either every page is drained or <paramref name="maxSeedRows"/>
    /// is reached.
    /// </summary>
    /// <remarks>
    /// Used for a polling trigger's very first poll instead of the normal
    /// <c>LastModifiedDate</c>-windowed incremental query. Fixes a real data-loss bug found during
    /// senior review (docs/dev-notes.md §0a): the original first poll only looked back 1 day, so any record
    /// not otherwise touched in the 24 hours before the automation started polling never got a
    /// baseline snapshot entry. When that record was later modified — including the exact change
    /// the trigger exists to catch — the "no previous value to compare against" rule (correctly
    /// used for genuinely brand-new records) silently ate the event instead, because it looked
    /// identical to "first time this record has ever been seen." Seeding from an unfiltered,
    /// paginated sweep of the whole object on poll #1 means every record that existed when the
    /// automation went live gets a real baseline, so only records created <em>after</em> that
    /// point can ever hit the "no previous value" path again — which is correct, since a record
    /// created after go-live genuinely has no prior state to diff against.
    /// </remarks>
    public static async Task<SalesforceSeedResult> SeedSnapshotAsync(
        SalesforceConnectionContext connection,
        ISalesforceClient client,
        string apiVersion,
        string objectApiName,
        string snapshotFieldApiName,
        int maxSeedRows,
        CancellationToken cancellationToken)
    {
        var snapshot = new Dictionary<string, string>();
        var soql = $"SELECT Id,{snapshotFieldApiName} FROM {objectApiName} ORDER BY Id";
        string? path = $"/services/data/{apiVersion}/query?q={Uri.EscapeDataString(soql)}";

        while (path is not null)
        {
            var result = await client.SendAsync(connection, HttpMethod.Get, path, jsonBody: null, cancellationToken);
            if (!result.IsSuccess || result.Json is not { } json)
            {
                // Leave LastPollUtc unset by returning Succeeded=false — the caller should retry
                // the whole seed from scratch next poll rather than persist a partial snapshot as
                // if it were complete.
                return new SalesforceSeedResult(Succeeded: false, Truncated: false, Snapshot: snapshot, Error: result.Error);
            }

            if (json.TryGetProperty("records", out var records))
            {
                foreach (var record in records.EnumerateArray())
                {
                    var fields = SalesforceJsonHelpers.ToFieldDictionary(record);
                    if (fields.TryGetValue("Id", out var idValue) && idValue is string id
                        && fields.TryGetValue(snapshotFieldApiName, out var fieldValue) && fieldValue is string value)
                    {
                        snapshot[id] = value;
                    }

                    if (snapshot.Count >= maxSeedRows)
                    {
                        return new SalesforceSeedResult(Succeeded: true, Truncated: true, Snapshot: snapshot, Error: null);
                    }
                }
            }

            var done = !json.TryGetProperty("done", out var doneElement) || doneElement.GetBoolean();
            path = done
                ? null
                : json.TryGetProperty("nextRecordsUrl", out var nextUrl) ? nextUrl.GetString() : null;
        }

        return new SalesforceSeedResult(Succeeded: true, Truncated: false, Snapshot: snapshot, Error: null);
    }

    /// <summary>
    /// Computes the polling watermark to persist for the next poll. Every SOQL-based polling
    /// trigger bounds its query with <c>LIMIT {MaxQueryRows}</c> to protect Salesforce governor
    /// limits — but if a poll actually hits that cap, more matching records may exist beyond it
    /// that this poll never saw. Advancing the watermark all the way to
    /// <paramref name="pollStartedUtc"/> in that case would make the next poll's lower bound skip
    /// past those unseen records, losing them permanently (found during the actions/triggers
    /// edge-case audit — docs/dev-notes.md §0a). Capping the watermark to the last processed record's own
    /// timestamp instead means the next poll resumes immediately after it, so a sustained burst
    /// drains over several poll cycles instead of silently dropping rows.
    /// </summary>
    /// <remarks>
    /// Residual edge case, not fully fixable without ID-based pagination: if multiple records
    /// share the exact same timestamp (to the millisecond) straddling the LIMIT boundary, the ones
    /// past the LIMIT could still be missed, since the next poll's <c>&gt;</c> comparison excludes
    /// anything equal to the new watermark. Rare enough in practice not to block this fix.
    /// </remarks>
    public static DateTime ComputeNextPollWatermark(
        int recordCount, int maxQueryRows, DateTime? lastRecordTimestamp, DateTime pollStartedUtc)
        => recordCount >= maxQueryRows && lastRecordTimestamp is { } last ? last : pollStartedUtc;
}
