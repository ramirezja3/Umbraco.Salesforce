using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Settings for the <see cref="GetRecordAction"/>.
/// </summary>
public sealed class GetRecordSettings
{
    /// <summary>
    /// Gets or sets the Salesforce object API name (e.g. "Lead", "Contact").
    /// </summary>
    [Field(Label = "Object", Description = "The Salesforce object API name, e.g. Lead, Contact, Opportunity.")]
    public string ObjectApiName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the record to retrieve.
    /// </summary>
    [Field(Label = "Record Id", Description = "The ID of the record to retrieve.", SortOrder = 1, SupportsBindings = true)]
    public string RecordId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a comma-separated list of field API names to return. Leave empty to return
    /// all fields on the object.
    /// </summary>
    [Field(Label = "Fields", Description = "Comma-separated field API names to return, e.g. Id,Name,Email. Leave empty for all fields.",
        SortOrder = 2)]
    public string? Fields { get; set; }
}
