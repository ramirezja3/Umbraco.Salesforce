namespace Umbraco.Automate.Salesforce.Connection;

/// <summary>
/// Common shape shared by <see cref="SalesforceConnectionSettings"/> (production) and
/// <see cref="SalesforceSandboxConnectionSettings"/> (sandbox) so actions can resolve the
/// underlying OAuth credential without caring which of the two connection types is configured
/// on a given step — see <see cref="SalesforceConnectionResolver"/>.
/// </summary>
public interface ISalesforceConnectionSettings
{
    /// <summary>
    /// Gets or sets the OAuth credential ID linking to the stored Salesforce tokens.
    /// </summary>
    Guid? OAuthCredentialsId { get; set; }
}
