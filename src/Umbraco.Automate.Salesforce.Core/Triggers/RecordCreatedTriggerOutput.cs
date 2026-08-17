namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Output produced by the <see cref="RecordCreatedTrigger"/>.
/// </summary>
public sealed class RecordCreatedTriggerOutput
{
    /// <summary>Gets the ID of the created record.</summary>
    public required string RecordId { get; init; }

    /// <summary>Gets the Salesforce object API name.</summary>
    public required string ObjectApiName { get; init; }

    /// <summary>Gets when the record was created.</summary>
    public required DateTime CreatedDateUtc { get; init; }

    /// <summary>
    /// Gets the record's field values (as configured via <see cref="RecordCreatedTriggerSettings.Fields"/>),
    /// keyed by field API name.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Fields { get; init; } = new Dictionary<string, object?>();
}
