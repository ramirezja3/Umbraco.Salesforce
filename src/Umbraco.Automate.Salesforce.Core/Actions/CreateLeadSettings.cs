using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Settings for the <see cref="CreateLeadAction"/>.
/// </summary>
/// <remarks>
/// A purpose-built convenience action for the single most common Salesforce write in web-form-to-CRM
/// automations (docs/dev-notes.md §11) — named fields for the common Lead attributes instead of the generic
/// <c>CreateRecordAction</c>'s raw JSON field map. For anything not covered here, use
/// <see cref="AdditionalFields"/> or fall back to <c>CreateRecordAction</c> with <c>ObjectApiName</c>
/// set to <c>Lead</c>.
/// </remarks>
public sealed class CreateLeadSettings
{
    /// <summary>
    /// Gets or sets the Lead's last name. Required by Salesforce.
    /// </summary>
    [Field(Label = "Last Name", Description = "The lead's last name.", SupportsBindings = true)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Lead's company name. Required by Salesforce unless the organization has Person
    /// Accounts enabled.
    /// </summary>
    [Field(Label = "Company", Description = "The lead's company name.", SortOrder = 1, SupportsBindings = true)]
    public string Company { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Lead's first name.
    /// </summary>
    [Field(Label = "First Name", Description = "The lead's first name.", SortOrder = 2, SupportsBindings = true)]
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the Lead's email address.
    /// </summary>
    [Field(Label = "Email", Description = "The lead's email address.", SortOrder = 3, SupportsBindings = true)]
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the Lead's phone number.
    /// </summary>
    [Field(Label = "Phone", Description = "The lead's phone number.", SortOrder = 4, SupportsBindings = true)]
    public string? Phone { get; set; }

    /// <summary>
    /// Gets or sets the Lead's job title.
    /// </summary>
    [Field(Label = "Title", Description = "The lead's job title.", SortOrder = 5, SupportsBindings = true)]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the Lead's source (e.g. "Web", "Phone Inquiry"). Must match one of the organization's
    /// configured Lead Source picklist values.
    /// </summary>
    [Field(Label = "Lead Source", Description = "The lead source, e.g. Web. Must match a configured picklist value in the organization.",
        SortOrder = 6, SupportsBindings = true)]
    public string? LeadSource { get; set; }

    /// <summary>
    /// Gets or sets the Lead's status. Leave empty to use the organization's default Lead status.
    /// </summary>
    [Field(Label = "Status", Description = "The lead status. Leave empty to use the organization's default.",
        SortOrder = 7, SupportsBindings = true)]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets additional field values not covered above, as a JSON object (e.g.
    /// <c>{"Industry": "Technology"}</c>). Merged with (and overridden by) the named fields above.
    /// </summary>
    [Field(Label = "Additional Fields", Description = "Additional field values as a JSON object, e.g. {\"Industry\": \"Technology\"}.",
        SortOrder = 8, SupportsBindings = true, EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
    public string? AdditionalFields { get; set; }
}
