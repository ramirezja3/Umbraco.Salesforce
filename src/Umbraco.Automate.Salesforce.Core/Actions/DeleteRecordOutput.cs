namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Output produced by the <see cref="DeleteRecordAction"/>.
/// </summary>
public sealed class DeleteRecordOutput
{
    /// <summary>
    /// Gets the ID of the deleted record (echoed from the input for convenience downstream).
    /// </summary>
    public required string RecordId { get; init; }
}
