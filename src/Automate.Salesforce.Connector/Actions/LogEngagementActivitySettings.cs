using Umbraco.Automate.Core.Settings;

namespace Automate.Salesforce.Connector.Actions;

/// <summary>
/// Settings for the <see cref="LogEngagementActivityAction"/>.
/// </summary>
public sealed class LogEngagementActivitySettings
{
    /// <summary>
    /// Gets or sets the Salesforce Id of the Contact or Lead this activity is logged against.
    /// Salesforce's <c>Task.WhoId</c> accepts either type directly.
    /// </summary>
    [Field(Label = "Contact or Lead Id", Description = "The Salesforce Id of the contact or lead this activity is logged against.",
        SupportsBindings = true)]
    public string WhoId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a short summary of what happened, e.g. "Website Engagement: Downloaded
    /// Pricing Guide".
    /// </summary>
    [Field(Label = "Subject", Description = "A short summary, e.g. Website Engagement: Downloaded Pricing Guide.",
        SortOrder = 1, SupportsBindings = true)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional longer description of the activity.
    /// </summary>
    [Field(Label = "Description", Description = "Optional longer detail about what happened.",
        SortOrder = 2, SupportsBindings = true, EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the date this happened, as <c>YYYY-MM-DD</c>. Leave empty to leave the date
    /// unset in Salesforce.
    /// </summary>
    [Field(Label = "Activity Date", Description = "The date this happened, as YYYY-MM-DD. Leave empty to leave it unset.",
        SortOrder = 3, SupportsBindings = true)]
    public string? ActivityDate { get; set; }
}
