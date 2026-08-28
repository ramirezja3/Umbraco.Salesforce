namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Output produced by the <see cref="LogEngagementActivityAction"/>.
/// </summary>
public sealed class LogEngagementActivityOutput
{
    /// <summary>
    /// Gets the Id of the created Task record.
    /// </summary>
    public string? RecordId { get; init; }
}
