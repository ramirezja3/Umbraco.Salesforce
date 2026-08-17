using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Settings for the <see cref="UpdateRecordAction"/>.
/// </summary>
public sealed class UpdateRecordSettings
{
    /// <summary>
    /// Gets or sets the Salesforce object API name (e.g. "Lead", "Contact").
    /// </summary>
    [Field(Label = "Object", Description = "The Salesforce object API name, e.g. Lead, Contact, Opportunity.")]
    public string ObjectApiName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the record to update.
    /// </summary>
    [Field(Label = "Record Id", Description = "The ID of the record to update.", SortOrder = 1, SupportsBindings = true)]
    public string RecordId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field values to set, as a JSON object. Values may use <c>${ }</c> bindings.
    /// </summary>
    [Field(Label = "Fields", Description = "Field values as a JSON object, e.g. {\"Status\": \"Closed\"}.",
        SortOrder = 2, SupportsBindings = true, EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
    public string Fields { get; set; } = "{}";
}
