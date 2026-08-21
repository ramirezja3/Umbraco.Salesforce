using Umbraco.Automate.OpenIddict.Providers;

// Schema wrapper consumed by the JsonSchemaGenerate MSBuild task at build time.
// Describes the appsettings.json shape below Umbraco:Automate:Providers:Salesforce /
// :SalesforceSandbox and Umbraco:Automate:Salesforce so tooling can give editors
// IntelliSense against appsettings-schema.Umbraco.Automate.Salesforce.json — mirrors
// Umbraco.Automate.Slack's UmbracoAutomateSlackSchema.cs.
internal sealed class UmbracoAutomateSalesforceSchema
{
    /// <summary>
    /// Configuration container for all Umbraco products.
    /// </summary>
    public required UmbracoDefinition Umbraco { get; set; }

    public sealed class UmbracoDefinition
    {
        /// <summary>
        /// Configuration of Umbraco Automate.
        /// </summary>
        public required UmbracoAutomateDefinition Automate { get; set; }
    }

    public sealed class UmbracoAutomateDefinition
    {
        /// <summary>
        /// OAuth provider credentials, keyed by provider name.
        /// </summary>
        public required ProvidersDefinition Providers { get; set; }

        /// <summary>
        /// Salesforce REST API call behaviour — distinct from the OAuth app credentials above.
        /// </summary>
        public required SalesforceApiDefinition Salesforce { get; set; }
    }

    public sealed class ProvidersDefinition
    {
        /// <summary>
        /// Salesforce production OAuth app credentials and scope configuration
        /// (issuer <c>https://login.salesforce.com/</c>).
        /// </summary>
        public required OAuthProviderConfiguration Salesforce { get; set; }

        /// <summary>
        /// Salesforce sandbox OAuth app credentials and scope configuration
        /// (issuer <c>https://test.salesforce.com/</c>).
        /// </summary>
        public required OAuthProviderConfiguration SalesforceSandbox { get; set; }
    }

    /// <summary>
    /// Salesforce API call behaviour section (<c>Umbraco:Automate:Salesforce</c>).
    /// </summary>
    public sealed class SalesforceApiDefinition
    {
        /// <summary>
        /// Gets or sets the Salesforce REST API version to call, e.g. <c>"v62.0"</c>.
        /// </summary>
        public string ApiVersion { get; set; } = "v62.0";

        /// <summary>
        /// Gets or sets the maximum rows a "Query Records (SOQL)" action may request in a
        /// single automation step, regardless of what the query text asks for.
        /// </summary>
        public int MaxQueryRows { get; set; } = 200;

        /// <summary>
        /// Gets or sets the maximum number of retry attempts on a rate-limited response before
        /// the action fails.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the maximum number of records a polling trigger's one-time initial seed
        /// sweep will fetch before giving up on the rest of the organization's records.
        /// </summary>
        public int MaxSeedRows { get; set; } = 50_000;

        /// <summary>
        /// Polling-trigger cadence (<c>Umbraco:Automate:Salesforce:Polling</c>).
        /// </summary>
        public SalesforcePollingDefinition Polling { get; set; } = new();
    }

    public sealed class SalesforcePollingDefinition
    {
        /// <summary>Gets or sets how often to poll. Defaults to one minute.</summary>
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>Gets or sets the delay before the first poll after startup.</summary>
        public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);
    }
}
