namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Output produced by the <see cref="RecordDeletedTrigger"/>.
/// </summary>
public sealed class RecordDeletedTriggerOutput
{
    /// <summary>Gets the ID of the deleted record.</summary>
    public required string RecordId { get; init; }

    /// <summary>Gets the Salesforce object API name.</summary>
    public required string ObjectApiName { get; init; }

    /// <summary>Gets when the record was deleted.</summary>
    public required DateTime DeletedDateUtc { get; init; }
}
