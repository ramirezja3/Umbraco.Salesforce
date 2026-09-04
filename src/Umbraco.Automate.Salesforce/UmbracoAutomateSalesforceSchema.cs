using Umbraco.Automate.OpenIddict.Providers;

// Schema wrapper consumed by the JsonSchemaGenerate MSBuild task at build time.
// Describes the appsettings.json shape below Umbraco:Automate:Providers:Salesforce and
// Umbraco:Automate:Salesforce so tooling can give editors IntelliSense against
// appsettings-schema.Umbraco.Automate.Salesforce.json — mirrors
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
        /// Salesforce OAuth app credentials and scope configuration
        /// (issuer <c>https://login.salesforce.com/</c>).
        /// </summary>
        public required OAuthProviderConfiguration Salesforce { get; set; }
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
        /// Gets or sets the maximum number of retry attempts on a rate-limited response before
        /// the action fails, after the first (non-retry) attempt.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the ceiling on how long a single retry delay is allowed to grow to.
        /// </summary>
        public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
    }
}
