using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Connection;

/// <summary>
/// Settings for the sandbox Salesforce connection type (authenticates against
/// <c>test.salesforce.com</c> — see <see cref="SalesforceSandboxConnectionType"/>).
/// </summary>
public sealed class SalesforceSandboxConnectionSettings : ISalesforceConnectionSettings
{
    /// <summary>
    /// Gets or sets the OAuth credential ID linking to the stored Salesforce sandbox tokens.
    /// </summary>
    [Field(Label = "Salesforce Sandbox",
        Description = "Authenticate with your Salesforce sandbox org",
        EditorUiAlias = "Umb.Automate.OAuth",
        EditorConfig = """[{ "alias": "provider", "value": "SalesforceSandbox" }]""")]
    public Guid? OAuthCredentialsId { get; set; }
}
