namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Output produced by the <see cref="LeadConvertedTrigger"/>.
/// </summary>
public sealed class LeadConvertedTriggerOutput
{
    /// <summary>Gets the converted Lead's ID.</summary>
    public required string LeadId { get; init; }

    /// <summary>Gets the resulting Contact's ID.</summary>
    public string? ConvertedContactId { get; init; }

    /// <summary>Gets the resulting Account's ID.</summary>
    public string? ConvertedAccountId { get; init; }

    /// <summary>Gets the resulting Opportunity's ID, if one was created.</summary>
    public string? ConvertedOpportunityId { get; init; }
}
