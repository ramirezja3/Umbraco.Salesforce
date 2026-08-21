using System.Collections.Concurrent;
using Umbraco.Automate.OpenIddict.Credentials;

namespace Umbraco.Automate.Salesforce.Connection;

/// <inheritdoc cref="ISalesforceConnectionResolver"/>
internal sealed class SalesforceConnectionResolver : ISalesforceConnectionResolver
{
    // How long a just-completed force-refresh is considered "fresh enough" for a second
    // concurrent caller to reuse instead of redeeming the refresh token again itself. See the
    // locking/dedup remarks on ForceRefreshAsync below — found during senior review (docs/dev-notes.md
    // §0a): two actions hitting INVALID_SESSION_ID at the same moment previously had no
    // coordination at all.
    private static readonly TimeSpan RecentRefreshWindow = TimeSpan.FromSeconds(5);

    private readonly IOAuthCredentialsService _credentialsService;

    // Registered as a singleton (see SalesforceComposer), so these instance-level collections are
    // effectively process-wide without needing to be static — one semaphore/cache entry per
    // distinct Salesforce connection ever resolved on this host, for the lifetime of the process.
    // That's a small, slow-growing set (bounded by how many Salesforce connections an implementer
    // actually creates), not an unbounded per-call allocation, so no eviction logic is needed.
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _refreshLocks = new();
    private readonly ConcurrentDictionary<Guid, (DateTime RefreshedUtc, SalesforceConnectionContext? Context)> _recentRefreshes = new();

    public SalesforceConnectionResolver(IOAuthCredentialsService credentialsService)
    {
        _credentialsService = credentialsService;
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
            _recentRefreshes[credentialsId] = (DateTime.UtcNow, refreshed);
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
            || !Uri.TryCreate(credentials.AccountLabel, UriKind.Absolute, out var instanceUrl))
        {
            return null;
        }

        return new SalesforceConnectionContext(credentialsId, accessToken, instanceUrl);
    }
}
