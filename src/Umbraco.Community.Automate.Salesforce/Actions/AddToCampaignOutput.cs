namespace Umbraco.Community.Automate.Salesforce.Actions;

/// <summary>
/// Output produced by the <see cref="AddToCampaignAction"/>.
/// </summary>
public sealed class AddToCampaignOutput
{
    /// <summary>
    /// Gets the Id of the created CampaignMember record.
    /// </summary>
    public string? RecordId { get; init; }
}
