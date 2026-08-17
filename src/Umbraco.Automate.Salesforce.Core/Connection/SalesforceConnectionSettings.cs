using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Salesforce.Connection;

/// <summary>
/// Settings for the production Salesforce connection type (authenticates against
/// <c>login.salesforce.com</c> — see <see cref="SalesforceConnectionType"/>).
/// </summary>
public sealed class SalesforceConnectionSettings : ISalesforceConnectionSettings
{
    /// <summary>
    /// Gets or sets the OAuth credential ID linking to the stored Salesforce org tokens.
    /// </summary>
    [Field(Label = "Salesforce Org",
        Description = "Authenticate with your Salesforce production org",
        EditorUiAlias = "Umb.Automate.OAuth",
        EditorConfig = """[{ "alias": "provider", "value": "Salesforce" }]""")]
    public Guid? OAuthCredentialsId { get; set; }
}
