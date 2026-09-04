namespace Umbraco.Community.Automate.Salesforce.Connection;

/// <summary>
/// Resolves an authenticated Salesforce OAuth credential down to a valid access token and the
/// organization's instance URL, ready for API calls.
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

    /// <summary>
    /// Forces a token refresh for the given credential and resolves a fresh access token and
    /// instance URL. Salesforce access tokens carry no <c>expires_in</c> in the standard Web
    /// Server flow response, so <c>IOAuthCredentialsService.GetValidAccessTokenAsync</c> never
    /// proactively refreshes them — it treats a token with no known expiry as always valid and
    /// only finds out otherwise when Salesforce itself rejects it (session timeout, revocation,
    /// IP-restriction change, etc.) with <c>INVALID_SESSION_ID</c>. This method exists so
    /// <see cref="Api.SalesforceClient"/> can recover from that instead of requiring the
    /// implementer to manually reconnect after every such Salesforce-side session invalidation.
    /// Returns <c>null</c> under the same conditions as <see cref="ResolveAsync"/> (e.g. the
    /// refresh token itself has been revoked) — callers should treat that as "reconnect required".
    /// Implementations must serialize concurrent calls for the same <paramref name="credentialsId"/>
    /// so two callers racing on the same stale session don't both redeem the refresh token —
    /// see the concrete <see cref="SalesforceConnectionResolver"/> for why that matters.
    /// </summary>
    Task<SalesforceConnectionContext?> ForceRefreshAsync(Guid credentialsId, CancellationToken cancellationToken);
}
