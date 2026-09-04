namespace Umbraco.Community.Automate.Salesforce.Actions;

/// <summary>
/// Output produced by the <see cref="UpdateOpportunityStageAction"/>.
/// </summary>
public sealed class UpdateOpportunityStageOutput
{
    /// <summary>
    /// Gets the Id of the updated Opportunity (echoes the configured Opportunity Id, since
    /// Salesforce returns no body on a successful update).
    /// </summary>
    public string? RecordId { get; init; }
}
