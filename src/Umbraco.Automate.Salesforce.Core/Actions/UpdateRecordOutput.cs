namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Output produced by the <see cref="UpdateRecordAction"/>.
/// </summary>
public sealed class UpdateRecordOutput
{
    /// <summary>
    /// Gets the ID of the updated record (echoed from the input for convenience downstream).
    /// </summary>
    public required string RecordId { get; init; }
}
