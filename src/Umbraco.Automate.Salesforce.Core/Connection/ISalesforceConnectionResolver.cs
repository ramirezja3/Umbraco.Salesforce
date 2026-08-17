namespace Umbraco.Automate.Salesforce.Connection;

/// <summary>
/// Resolves an authenticated Salesforce OAuth credential (production or sandbox) down to a
/// valid access token and the org's instance URL, ready for API calls.
/// </summary>
public interface ISalesforceConnectionResolver
{
    /// <summary>
    /// Resolves the given credential's access token and instance URL.
    /// Returns <c>null</c> if the credential doesn't exist, the token is expired/revoked and
    /// cannot be refreshed, or no instance URL was captured at authentication time (see
    /// <see cref="Configuration.SalesforceOAuthHandlers"/>) — callers should surface this as an
    /// "expired or revoked, please re-authenticate" failure, mirroring
    /// <c>Umbraco.Automate.Slack</c>'s <c>SendMessageAction</c>.
    /// </summary>
    Task<SalesforceConnectionContext?> ResolveAsync(Guid credentialsId, CancellationToken cancellationToken);
}
