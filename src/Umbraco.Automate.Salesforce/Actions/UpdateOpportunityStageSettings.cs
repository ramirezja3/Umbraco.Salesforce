using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Settings for the <see cref="UpdateOpportunityStageAction"/>.
/// </summary>
public sealed class UpdateOpportunityStageSettings
{
    /// <summary>
    /// Gets or sets the Salesforce Id of the Opportunity to update.
    /// </summary>
    [Field(Label = "Opportunity Id", Description = "The Salesforce Id of the opportunity to update.", SupportsBindings = true)]
    public string OpportunityId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target stage. Must match one of the organization's configured
    /// Opportunity Stage picklist values, e.g. "Closed Won".
    /// </summary>
    [Field(Label = "Stage", Description = "The target stage, e.g. Closed Won. Must match a configured picklist value in the organization.",
        SortOrder = 1, SupportsBindings = true)]
    public string StageName { get; set; } = string.Empty;
}
