using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Settings for the <see cref="RecordDeletedTrigger"/>.
/// </summary>
public sealed class RecordDeletedTriggerSettings
{
    /// <summary>
    /// Gets or sets the Salesforce object API name to watch.
    /// </summary>
    [Field(Label = "Object", Description = "The Salesforce object API name to watch, e.g. Lead, Contact.")]
    public string ObjectApiName { get; set; } = string.Empty;
}
