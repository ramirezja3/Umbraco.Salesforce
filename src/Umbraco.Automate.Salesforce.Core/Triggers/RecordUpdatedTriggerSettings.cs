using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Settings for the <see cref="RecordUpdatedTrigger"/>.
/// </summary>
public sealed class RecordUpdatedTriggerSettings
{
    /// <summary>
    /// Gets or sets the Salesforce object API name to watch.
    /// </summary>
    [Field(Label = "Object", Description = "The Salesforce object API name to watch, e.g. Lead, Contact.")]
    public string ObjectApiName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a comma-separated list of additional field API names to include in the
    /// output (Id and LastModifiedDate are always included).
    /// </summary>
    /// <remarks>
    /// v1 fires on <em>any</em> field changing (any poll where LastModifiedDate has advanced),
    /// not specific fields — narrowing to "did field X specifically change" needs the same
    /// snapshot-diffing <see cref="OpportunityStageChangedTrigger"/> uses, and isn't built
    /// generically for arbitrary fields in this pass.
    /// </remarks>
    [Field(Label = "Fields", Description = "Comma-separated field API names to include, e.g. Name,Status,Email.",
        SortOrder = 1)]
    public string? Fields { get; set; }
}
