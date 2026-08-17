namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Output produced by the <see cref="QueryRecordsAction"/>.
/// </summary>
public sealed class QueryRecordsOutput
{
    /// <summary>
    /// Gets the matching records, each as a field-name → value map.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Records { get; init; } = [];

    /// <summary>
    /// Gets the total number of rows Salesforce reports matching the query — this can be larger
    /// than <see cref="Records"/>.Count when the result was truncated by the row cap; see
    /// <see cref="Truncated"/>.
    /// </summary>
    public required int TotalSize { get; init; }

    /// <summary>
    /// Gets a value indicating whether more rows matched the query than were returned (v1 does
    /// not follow Salesforce's <c>nextRecordsUrl</c> pagination — see <see cref="QueryRecordsAction"/>).
    /// When <c>true</c>, narrow the query or raise the row cap rather than assuming this is the
    /// full result set.
    /// </summary>
    public required bool Truncated { get; init; }
}
