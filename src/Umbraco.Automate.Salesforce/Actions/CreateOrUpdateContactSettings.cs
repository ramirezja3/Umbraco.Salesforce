using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Settings for the <see cref="CreateOrUpdateContactAction"/>.
/// </summary>
public sealed class CreateOrUpdateContactSettings
{
    /// <summary>
    /// Gets or sets the Contact's last name. Required by Salesforce.
    /// </summary>
    [Field(Label = "Last Name", Description = "The contact's last name.", SupportsBindings = true)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Contact's first name.
    /// </summary>
    [Field(Label = "First Name", Description = "The contact's first name.", SortOrder = 1, SupportsBindings = true)]
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the Contact's email address.
    /// </summary>
    [Field(Label = "Email", Description = "The contact's email address.", SortOrder = 2, SupportsBindings = true)]
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the Contact's phone number.
    /// </summary>
    [Field(Label = "Phone", Description = "The contact's phone number.", SortOrder = 3, SupportsBindings = true)]
    public string? Phone { get; set; }

    /// <summary>
    /// Gets or sets the Salesforce Id of the Account (company) this Contact belongs to. Not
    /// required by the Salesforce platform itself, but many organizations require it via their
    /// own validation rules.
    /// </summary>
    [Field(Label = "Account Id", Description = "The Salesforce Id of the Account this contact belongs to. Some organizations require this.",
        SortOrder = 4, SupportsBindings = true)]
    public string? AccountId { get; set; }

    /// <summary>
    /// Gets or sets the Salesforce Id of an existing Contact to update. Leave empty to create a
    /// new Contact instead.
    /// </summary>
    [Field(Label = "Contact Id (to update)", Description = "Leave empty to create a new Contact. Set to an existing Contact's Id to update it instead.",
        SortOrder = 5, SupportsBindings = true)]
    public string? ContactId { get; set; }

    /// <summary>
    /// Gets or sets additional field values not covered above, as a JSON object (e.g.
    /// <c>{"Department": "Engineering"}</c>). Merged with (and overridden by) the named fields above.
    /// </summary>
    [Field(Label = "Additional Fields", Description = "Additional field values as a JSON object, e.g. {\"Department\": \"Engineering\"}.",
        SortOrder = 6, SupportsBindings = true, EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
    public string? AdditionalFields { get; set; }
}
