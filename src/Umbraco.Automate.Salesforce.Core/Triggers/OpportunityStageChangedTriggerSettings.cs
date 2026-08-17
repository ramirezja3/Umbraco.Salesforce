using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Settings for the <see cref="OpportunityStageChangedTrigger"/>.
/// </summary>
public sealed class OpportunityStageChangedTriggerSettings
{
    /// <summary>
    /// Gets or sets the target stage to filter on (e.g. "Closed Won"). Leave empty to fire on
    /// any stage change.
    /// </summary>
    [Field(Label = "Target Stage", Description = "Only fire when the new stage matches this value, e.g. Closed Won. Leave empty to fire on any stage change.")]
    public string? TargetStage { get; set; }
}
