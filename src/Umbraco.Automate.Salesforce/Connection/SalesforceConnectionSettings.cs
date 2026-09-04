using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Connection;

/// <summary>
/// Settings for the Salesforce connection type (authenticates against
/// <c>login.salesforce.com</c> — see <see cref="SalesforceConnectionType"/>).
/// </summary>
public sealed class SalesforceConnectionSettings
{
    /// <summary>
    /// Gets or sets the OAuth credential ID linking to the stored Salesforce organization tokens.
    /// </summary>
    [Field(Label = "Salesforce Organization",
        Description = "Authenticate with your Salesforce organization",
        EditorUiAlias = "Umb.Automate.OAuth",
        EditorConfig = """[{ "alias": "provider", "value": "Salesforce" }]""")]
    public Guid? OAuthCredentialsId { get; set; }
}
