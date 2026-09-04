using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.OpenIddict.Credentials;

namespace Automate.Salesforce.Connector.Connection;

/// <inheritdoc cref="ISalesforceConnectionResolver"/>
internal sealed class SalesforceConnectionResolver : ISalesforceConnectionResolver
{
    // How long a just-completed force-refresh is considered "fresh enough" for a second
    // concurrent caller to reuse instead of redeeming the refresh token again itself — see the
    // locking/dedup remarks on ForceRefreshAsync below. This coordinates two actions that hit
    // INVALID_SESSION_ID at the same moment so they don't both redeem the refresh token.
    private static readonly TimeSpan RecentRefreshWindow = TimeSpan.FromSeconds(5);

    private readonly IOAuthCredentialsService _credentialsService;
    private readonly ILogger<SalesforceConnectionResolver> _logger;

    // Registered as a singleton (see SalesforceComposer), so these instance-level collections are
    // effectively process-wide without needing to be static — one semaphore/cache entry per
    // distinct Salesforce connection ever resolved on this host, for the lifetime of the process.
    // That's a small, slow-growing set (bounded by how many Salesforce connections an implementer
    // actually creates), not an unbounded per-call allocation, so no eviction logic is needed.
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _refreshLocks = new();
    // Only successful refreshes are ever stored here (see ForceRefreshAsync) — a failed refresh
    // is never cached, so a transient blip doesn't get replayed as a failure to every other
    // concurrent caller for the rest of the freshness window.
    private readonly ConcurrentDictionary<Guid, (DateTime RefreshedUtc, SalesforceConnectionContext Context)> _recentRefreshes = new();

    public SalesforceConnectionResolver(IOAuthCredentialsService credentialsService, ILogger<SalesforceConnectionResolver> logger)
    {
        _credentialsService = credentialsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<SalesforceConnectionContext?> ResolveAsync(Guid credentialsId, CancellationToken cancellationToken)
        => ResolveCoreAsync(credentialsId, forceRefresh: false, cancellationToken);

    /// <summary>
    /// Forces a token refresh for <paramref name="credentialsId"/> and resolves the refreshed
    /// connection. Serialized per <paramref name="credentialsId"/>: if two callers force-refresh
    /// the same credential at nearly the same moment (e.g. two automation steps both hitting
    /// <c>INVALID_SESSION_ID</c> at once — see <see cref="Api.SalesforceClient"/>'s single-retry
    /// design), the second caller reuses the first caller's just-completed refresh instead of
    /// redeeming the refresh token a second time in quick succession. That matters because some
    /// Salesforce Connected App Refresh Token Policies rotate/invalidate the previous refresh
    /// token on each use — without this, the loser of that race could get a revoked refresh token
    /// and need a full manual reconnect, purely because two automations happened to run at once.
    /// </summary>
    public async Task<SalesforceConnectionContext?> ForceRefreshAsync(Guid credentialsId, CancellationToken cancellationToken)
    {
        var gate = _refreshLocks.GetOrAdd(credentialsId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (_recentRefreshes.TryGetValue(credentialsId, out var recent)
                && DateTime.UtcNow - recent.RefreshedUtc < RecentRefreshWindow)
            {
                return recent.Context;
            }

            var refreshed = await ResolveCoreAsync(credentialsId, forceRefresh: true, cancellationToken);
            if (refreshed is not null)
            {
                _recentRefreshes[credentialsId] = (DateTime.UtcNow, refreshed);
            }

            return refreshed;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<SalesforceConnectionContext?> ResolveCoreAsync(Guid credentialsId, bool forceRefresh, CancellationToken cancellationToken)
    {
        if (forceRefresh)
        {
            // IOAuthCredentialsService.GetValidAccessTokenAsync only refreshes when the stored
            // ExpiresUtc has passed. Salesforce's Web Server flow token response carries no
            // expires_in, so ExpiresUtc stays null forever and that check never trips on its own
            // (see ISalesforceConnectionResolver.ForceRefreshAsync doc). Marking it expired here
            // — via the service's own public UpdateCredentialsAsync, not by touching
            // Umbraco.Automate.OpenIddict internals — makes the very next GetValidAccessTokenAsync
            // call take the refresh-token branch.
            var existing = await _credentialsService.GetCredentialsAsync(credentialsId, cancellationToken);
            if (existing is null)
            {
                return null;
            }

            existing.ExpiresUtc = DateTime.UtcNow.AddMinutes(-1);
            await _credentialsService.UpdateCredentialsAsync(existing, cancellationToken);
        }

        var accessToken = await _credentialsService.GetValidAccessTokenAsync(credentialsId, cancellationToken);
        if (string.IsNullOrEmpty(accessToken))
        {
            return null;
        }

        var credentials = await _credentialsService.GetCredentialsAsync(credentialsId, cancellationToken);

        // The instance URL is captured from the token response at authentication time and
        // stashed in the generic AccountLabel column — see SalesforceOAuthHandlers.ExtractInstanceUrl.
        // There is no Salesforce-specific persistence for this: AccountLabel already exists on
        // Umbraco.Automate.OpenIddict's credential entity and needs no schema change to reuse.
        if (string.IsNullOrEmpty(credentials?.AccountLabel)
            || !Uri.TryCreate(credentials.AccountLabel, UriKind.Absolute, out var instanceUrl)
            || !IsTrustedSalesforceHost(instanceUrl))
        {
            // A non-Salesforce (or non-HTTPS) instance_url should never reach here in normal
            // operation — Salesforce's own token endpoint is the only source for AccountLabel
            // (see ExtractInstanceUrl). This check exists purely as defense in depth: every
            // action attaches the live Bearer access token to whatever host InstanceUrl resolves
            // to (see SalesforceClient.SendAsync), so a corrupted, tampered-with, or otherwise
            // untrustworthy stored value must never be used to build an authenticated outbound
            // request — that would hand a real Salesforce access token to an arbitrary host.
            _logger.LogWarning(
                "Rejected untrusted Salesforce instance URL for credentials {CredentialsId} — expected an HTTPS *.salesforce.com or *.force.com host.",
                credentialsId);
            return null;
        }

        return new SalesforceConnectionContext(credentialsId, accessToken, instanceUrl);
    }

    /// <summary>
    /// Restricts which hosts this package will ever attach a live Salesforce Bearer token to.
    /// Salesforce's documented instance URL hosts are HTTPS subdomains of <c>salesforce.com</c>
    /// (e.g. <c>yourdomain.my.salesforce.com</c>, legacy pod hosts like <c>na1.salesforce.com</c>)
    /// or <c>force.com</c> (Experience Cloud / Force.com sites). Anything else is rejected rather
    /// than trusted.
    /// </summary>
    private static bool IsTrustedSalesforceHost(Uri instanceUrl)
    {
        if (instanceUrl.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = instanceUrl.Host;
        return host.Equals("salesforce.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".salesforce.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("force.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".force.com", StringComparison.OrdinalIgnoreCase);
    }
}
