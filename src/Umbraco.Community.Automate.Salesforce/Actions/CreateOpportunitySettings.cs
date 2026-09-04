using Umbraco.Automate.Core.Settings;

namespace Umbraco.Community.Automate.Salesforce.Actions;

/// <summary>
/// Settings for the <see cref="CreateOpportunityAction"/>.
/// </summary>
public sealed class CreateOpportunitySettings
{
    /// <summary>
    /// Gets or sets the Opportunity's name. Required by Salesforce.
    /// </summary>
    [Field(Label = "Name", Description = "A name for this opportunity, e.g. the order or deal reference.", SupportsBindings = true)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Opportunity's stage. Must match one of the organization's configured
    /// Opportunity Stage picklist values, e.g. "Prospecting" or "Closed Won".
    /// </summary>
    [Field(Label = "Stage", Description = "The opportunity stage, e.g. Prospecting. Must match a configured picklist value in the organization.",
        SortOrder = 1, SupportsBindings = true)]
    public string StageName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expected close date, as <c>YYYY-MM-DD</c>. Required by Salesforce.
    /// </summary>
    [Field(Label = "Close Date", Description = "The expected close date, as YYYY-MM-DD.", SortOrder = 2, SupportsBindings = true)]
    public string CloseDate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Salesforce Id of the associated Account (company). Not required by the
    /// Salesforce platform itself, but many organizations require it via their own validation
    /// rules.
    /// </summary>
    [Field(Label = "Account Id", Description = "The Salesforce Id of the associated Account. Some organizations require this.",
        SortOrder = 3, SupportsBindings = true)]
    public string? AccountId { get; set; }

    /// <summary>
    /// Gets or sets the deal amount.
    /// </summary>
    [Field(Label = "Amount", Description = "The opportunity's amount.", SortOrder = 4, SupportsBindings = true)]
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets additional field values not covered above, as a JSON object (e.g.
    /// <c>{"LeadSource": "Website"}</c>). Merged with (and overridden by) the named fields above.
    /// </summary>
    [Field(Label = "Additional Fields", Description = "Additional field values as a JSON object, e.g. {\"LeadSource\": \"Website\"}.",
        SortOrder = 5, SupportsBindings = true, EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
    public string? AdditionalFields { get; set; }
}
