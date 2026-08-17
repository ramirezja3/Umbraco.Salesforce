using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Settings for the <see cref="DeleteRecordAction"/>.
/// </summary>
public sealed class DeleteRecordSettings
{
    /// <summary>
    /// Gets or sets the Salesforce object API name (e.g. "Lead", "Contact").
    /// </summary>
    [Field(Label = "Object", Description = "The Salesforce object API name, e.g. Lead, Contact, Opportunity.")]
    public string ObjectApiName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the record to delete.
    /// </summary>
    [Field(Label = "Record Id", Description = "The ID of the record to delete.", SortOrder = 1, SupportsBindings = true)]
    public string RecordId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the destructive delete is explicitly confirmed.
    /// Must be set to <c>true</c> for the action to run — reduces accidental destructive
    /// automations (CLAUDE.md §7/§8).
    /// </summary>
    [Field(Label = "Confirm Delete", Description = "This action is destructive and cannot be undone. Confirm to enable it.",
        SortOrder = 2)]
    public bool ConfirmDelete { get; set; }
}
