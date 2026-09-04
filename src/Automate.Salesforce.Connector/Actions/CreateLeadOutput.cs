namespace Automate.Salesforce.Connector.Actions;

/// <summary>
/// Output produced by the <see cref="CreateLeadAction"/>.
/// </summary>
public sealed class CreateLeadOutput
{
    /// <summary>
    /// Gets the ID of the created Lead.
    /// </summary>
    public string? RecordId { get; init; }
}
