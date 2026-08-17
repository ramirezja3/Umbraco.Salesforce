namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Output produced by the <see cref="OpportunityStageChangedTrigger"/>.
/// </summary>
public sealed class OpportunityStageChangedTriggerOutput
{
    /// <summary>Gets the Opportunity's ID.</summary>
    public required string OpportunityId { get; init; }

    /// <summary>Gets the stage before this change.</summary>
    public required string PreviousStage { get; init; }

    /// <summary>Gets the stage after this change.</summary>
    public required string NewStage { get; init; }

    /// <summary>Gets the Opportunity's Amount, if set.</summary>
    public double? Amount { get; init; }

    /// <summary>Gets the Opportunity's AccountId, if set.</summary>
    public string? AccountId { get; init; }

    /// <summary>Gets the Opportunity's OwnerId.</summary>
    public string? OwnerId { get; init; }
}
