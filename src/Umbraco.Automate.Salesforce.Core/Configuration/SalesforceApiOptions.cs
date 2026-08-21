namespace Umbraco.Automate.Salesforce.Configuration;

/// <summary>
/// Options for calling the Salesforce REST API, bound from
/// <c>Umbraco:Automate:Salesforce</c> (distinct from the per-provider OAuth app credentials at
/// <c>Umbraco:Automate:Providers:Salesforce</c> / <c>:SalesforceSandbox</c>, which
/// <c>Umbraco.Automate.OpenIddict</c> binds generically).
/// </summary>
public sealed class SalesforceApiOptions
{
    /// <summary>
    /// Gets or sets the Salesforce REST API version to call, e.g. <c>"v62.0"</c>.
    /// Defaults to whatever was the latest stable version at the time this package was built —
    /// confirm against current Salesforce documentation before relying on the default long-term
    /// (see docs/dev-notes.md §13.3).
    /// </summary>
    public string ApiVersion { get; set; } = "v62.0";

    /// <summary>
    /// Gets or sets the maximum rows a "Query Records (SOQL)" action may request in a single
    /// automation step, regardless of what the query text asks for.
    /// </summary>
    public int MaxQueryRows { get; set; } = 200;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts on a rate-limited (429 /
    /// <c>REQUEST_LIMIT_EXCEEDED</c>) response before the action fails.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of records a polling trigger's one-time initial seed sweep
    /// (see <c>SalesforceTriggerSupport.SeedSnapshotAsync</c>) will fetch before giving up on the
    /// rest of the organization's records. Only spent once, on an automation's very first poll —
    /// each 2,000-record page costs one Salesforce API call, so the default of 50,000 costs at
    /// most 25 calls against the organization's daily API allocation, one time, per automation.
    /// </summary>
    public int MaxSeedRows { get; set; } = 50_000;
}
