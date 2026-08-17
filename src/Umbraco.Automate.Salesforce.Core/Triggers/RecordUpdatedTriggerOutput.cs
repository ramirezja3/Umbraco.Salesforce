namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Output produced by the <see cref="RecordUpdatedTrigger"/>.
/// </summary>
public sealed class RecordUpdatedTriggerOutput
{
    /// <summary>Gets the ID of the updated record.</summary>
    public required string RecordId { get; init; }

    /// <summary>Gets the Salesforce object API name.</summary>
    public required string ObjectApiName { get; init; }

    /// <summary>Gets when the record was last modified.</summary>
    public required DateTime LastModifiedDateUtc { get; init; }

    /// <summary>
    /// Gets the record's current field values (a snapshot, not an old/new diff — see
    /// <see cref="RecordUpdatedTriggerSettings"/> remarks), keyed by field API name.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Fields { get; init; } = new Dictionary<string, object?>();
}
