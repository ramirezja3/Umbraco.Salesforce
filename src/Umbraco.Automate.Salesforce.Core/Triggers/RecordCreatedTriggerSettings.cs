using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Settings for the <see cref="RecordCreatedTrigger"/>.
/// </summary>
public sealed class RecordCreatedTriggerSettings
{
    /// <summary>
    /// Gets or sets the Salesforce object API name to watch (e.g. "Lead", "Contact",
    /// "My_Custom_Object__c"). Plain text in v1 — no live object picker yet, see CLAUDE.md §0a.
    /// </summary>
    [Field(Label = "Object", Description = "The Salesforce object API name to watch, e.g. Lead, Contact.")]
    public string ObjectApiName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a comma-separated list of additional field API names to include in the
    /// output (Id and CreatedDate are always included).
    /// </summary>
    [Field(Label = "Fields", Description = "Comma-separated field API names to include, e.g. Name,Company,Email.",
        SortOrder = 1)]
    public string? Fields { get; set; }
}
