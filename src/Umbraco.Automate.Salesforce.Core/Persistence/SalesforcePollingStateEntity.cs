namespace Umbraco.Automate.Salesforce.Persistence;

/// <summary>
/// Per-automation polling checkpoint for Salesforce polling triggers (see
/// <c>Umbraco.Automate.Salesforce.Triggers.ISalesforcePollingTrigger</c>). One row per automation
/// — triggers have no Connection concept in this platform version (see CLAUDE.md §0a), so this
/// table is the only thing distinguishing "where a given automation's polling last left off."
/// </summary>
internal sealed class SalesforcePollingStateEntity
{
    /// <summary>Gets or sets the automation this checkpoint belongs to.</summary>
    public Guid AutomationId { get; set; }

    /// <summary>Gets or sets the exclusive upper bound of the last successful poll window.</summary>
    public DateTime? LastPollUtc { get; set; }

    /// <summary>
    /// Gets or sets a small JSON object of recordId → last-known-value, used only by triggers
    /// that need to detect a specific field changing (e.g. Opportunity Stage Changed) rather
    /// than just "this record was touched." Empty/null for triggers that don't need it.
    /// </summary>
    public string? SnapshotJson { get; set; }

    /// <summary>Gets or sets when this checkpoint was last written.</summary>
    public DateTime DateModified { get; set; } = DateTime.UtcNow;
}
