using Umbraco.Automate.Core.Settings;

namespace Automate.Salesforce.Connector.Actions;

/// <summary>
/// Settings for the <see cref="AddToCampaignAction"/>.
/// </summary>
public sealed class AddToCampaignSettings
{
    /// <summary>
    /// Gets or sets the Salesforce Id of the Campaign to add this person to.
    /// </summary>
    [Field(Label = "Campaign Id", Description = "The Salesforce Id of the campaign.", SupportsBindings = true)]
    public string CampaignId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Salesforce Id of the Contact to add. Provide exactly one of this or
    /// <see cref="LeadId"/>, not both.
    /// </summary>
    [Field(Label = "Contact Id", Description = "The Salesforce Id of the contact to add. Provide either this or a Lead Id, not both.",
        SortOrder = 1, SupportsBindings = true)]
    public string? ContactId { get; set; }

    /// <summary>
    /// Gets or sets the Salesforce Id of the Lead to add. Provide exactly one of this or
    /// <see cref="ContactId"/>, not both.
    /// </summary>
    [Field(Label = "Lead Id", Description = "The Salesforce Id of the lead to add. Provide either this or a Contact Id, not both.",
        SortOrder = 2, SupportsBindings = true)]
    public string? LeadId { get; set; }

    /// <summary>
    /// Gets or sets the campaign member status (e.g. "Sent", "Responded"). Campaign member
    /// status values are defined per-campaign in Salesforce Setup, not a global picklist — leave
    /// empty to use the campaign's own configured default status.
    /// </summary>
    [Field(Label = "Status", Description = "The campaign member status, e.g. Responded. Defined per-campaign in Setup — leave empty to use the campaign's default.",
        SortOrder = 3, SupportsBindings = true)]
    public string? Status { get; set; }
}
