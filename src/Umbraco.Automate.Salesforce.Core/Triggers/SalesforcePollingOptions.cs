namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Options for <see cref="SalesforcePollingBackgroundJob"/>, bound from
/// <c>Umbraco:Automate:Salesforce:Polling</c>.
/// </summary>
public sealed class SalesforcePollingOptions
{
    /// <summary>Gets or sets how often to poll. Defaults to one minute.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the delay before the first poll after startup.</summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);
}
