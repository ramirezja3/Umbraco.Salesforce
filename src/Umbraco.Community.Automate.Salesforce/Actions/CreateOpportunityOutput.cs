namespace Umbraco.Community.Automate.Salesforce.Actions;

/// <summary>
/// Output produced by the <see cref="CreateOpportunityAction"/>.
/// </summary>
public sealed class CreateOpportunityOutput
{
    /// <summary>
    /// Gets the Id of the created Opportunity.
    /// </summary>
    public string? RecordId { get; init; }
}
