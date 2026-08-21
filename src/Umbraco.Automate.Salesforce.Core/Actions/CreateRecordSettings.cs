using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Settings for the <see cref="CreateRecordAction"/>.
/// </summary>
public sealed class CreateRecordSettings
{
    /// <summary>
    /// Gets or sets the Salesforce object API name to create a record of (e.g. "Lead", "Contact",
    /// "My_Custom_Object__c"). Plain text in v1 — no live object picker yet, see docs/dev-notes.md §0a.
    /// </summary>
    [Field(Label = "Object", Description = "The Salesforce object API name, e.g. Lead, Contact, Opportunity.")]
    public string ObjectApiName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field values to set, as a JSON object
    /// (e.g. <c>{"LastName": "Smith", "Company": "Acme"}</c>). Values may use <c>${ }</c> bindings.
    /// </summary>
    [Field(Label = "Fields", Description = "Field values as a JSON object, e.g. {\"LastName\": \"Smith\", \"Company\": \"Acme\"}.",
        SortOrder = 1, SupportsBindings = true, EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
    public string Fields { get; set; } = "{}";
}
