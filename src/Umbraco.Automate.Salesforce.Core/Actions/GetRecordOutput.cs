namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Output produced by the <see cref="GetRecordAction"/>.
/// </summary>
public sealed class GetRecordOutput
{
    /// <summary>
    /// Gets a value indicating whether the record was found.
    /// </summary>
    public required bool Found { get; init; }

    /// <summary>
    /// Gets the record's field values, keyed by field API name. Empty when <see cref="Found"/>
    /// is <c>false</c>. Loosely typed in v1 — there's no live Describe-metadata schema yet
    /// (CLAUDE.md §0a), so values come back exactly as Salesforce's JSON typed them.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Fields { get; init; } = new Dictionary<string, object?>();
}
