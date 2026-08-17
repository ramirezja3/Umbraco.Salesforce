namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Output produced by the <see cref="CreateRecordAction"/>.
/// </summary>
public sealed class CreateRecordOutput
{
    /// <summary>
    /// Gets the ID of the created record.
    /// </summary>
    public string? RecordId { get; init; }
}
