using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Settings for the <see cref="UpsertRecordAction"/> — the idempotent, retry-safe write path
/// (CLAUDE.md §2 non-negotiable #10). Prefer this over Create Record wherever an external ID
/// field is available, so a retried automation step can't create duplicate records.
/// </summary>
public sealed class UpsertRecordSettings
{
    /// <summary>
    /// Gets or sets the Salesforce object API name (e.g. "Lead", "Contact").
    /// </summary>
    [Field(Label = "Object", Description = "The Salesforce object API name, e.g. Lead, Contact, Opportunity.")]
    public string ObjectApiName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the External ID field API name to upsert against (must be marked as an
    /// External ID field in Salesforce's object setup).
    /// </summary>
    [Field(Label = "External Id Field", Description = "The External ID field API name to match on, e.g. External_Id__c.",
        SortOrder = 1)]
    public string ExternalIdField { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external ID value identifying the record.
    /// </summary>
    [Field(Label = "External Id Value", Description = "The value to match against the External ID field.",
        SortOrder = 2, SupportsBindings = true)]
    public string ExternalIdValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field values to set, as a JSON object. Values may use <c>${ }</c> bindings.
    /// </summary>
    [Field(Label = "Fields", Description = "Field values as a JSON object, e.g. {\"LastName\": \"Smith\"}.",
        SortOrder = 3, SupportsBindings = true, EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
    public string Fields { get; set; } = "{}";
}
