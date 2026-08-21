using System.Collections.Immutable;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using static OpenIddict.Client.OpenIddictClientEvents;
using static OpenIddict.Client.WebIntegration.OpenIddictClientWebIntegrationConstants;

namespace Umbraco.Automate.Salesforce.Configuration;

/// <summary>
/// Custom OpenIddict event handler that captures Salesforce's non-standard <c>instance_url</c>
/// token response field — the organization's actual API base URL, which is <em>not</em> the login host
/// used to authenticate (see docs/dev-notes.md §0a). Salesforce's OAuth flow is otherwise standard OIDC,
/// so — unlike <c>Umbraco.Automate.Slack</c>'s <c>SlackOAuthHandlers</c> — no endpoint overrides
/// are needed here, just this one extra extraction.
/// </summary>
internal static class SalesforceOAuthHandlers
{
    public static ImmutableArray<OpenIddictClientHandlerDescriptor> Descriptors { get; } =
    [
        ExtractInstanceUrl.Descriptor,
    ];

    private static bool IsSalesforce(OpenIddictClientRegistration registration)
        => registration.ProviderType == ProviderTypes.Salesforce;

    /// <summary>
    /// Reads <c>instance_url</c> from the raw token response and stashes it as the account
    /// label so the generic OAuth callback controller persists it on the credential row — the
    /// same generic <c>AccountLabel</c> column <c>Umbraco.Automate.Slack</c> uses for its team
    /// name, repurposed here since there's no Salesforce-specific column to add it to without
    /// modifying <c>Umbraco.Automate.OpenIddict</c> (not allowed — see docs/dev-notes.md §0).
    /// <see cref="Connection.SalesforceConnectionResolver"/> reads it back out at call time.
    /// </summary>
    internal sealed class ExtractInstanceUrl : IOpenIddictClientHandler<ProcessAuthenticationContext>
    {
        public static OpenIddictClientHandlerDescriptor Descriptor { get; }
            = OpenIddictClientHandlerDescriptor.CreateBuilder<ProcessAuthenticationContext>()
                .UseSingletonHandler<ExtractInstanceUrl>()
                .SetOrder(int.MaxValue - 100_000)
                .Build();

        public ValueTask HandleAsync(ProcessAuthenticationContext context)
        {
            // Only captured on the initial interactive login (authorization_code), not on later
            // token refreshes. This was investigated during senior review (docs/dev-notes.md §0a) as a
            // possible staleness risk after a Salesforce organization-instance migration, and a
            // fix was attempted (also handling the refresh_token grant here) — but it turned out
            // to be a dead no-op: OAuthCredentialsService.RefreshAccessTokenAsync calls
            // AuthenticateWithRefreshTokenAsync directly (no HttpContext, no AuthenticationProperties),
            // whose result exposes only AccessToken/RefreshToken/AccessTokenExpirationDate — nothing
            // reads context.Properties[AccountLabel] back out on that path. Only the interactive
            // callback controller (OAuthCallbackController, via HttpContext.AuthenticateAsync's
            // AuthenticationProperties.Items) ever persists AccountLabel. Closing this for real
            // would require a change inside Umbraco.Automate.OpenIddict, which is out of scope for
            // this package (docs/dev-notes.md §0) — so this is left authorization_code-only, and the
            // residual staleness risk (an org migrates its instance host without the implementer
            // re-authenticating) is documented as a known limitation in docs/troubleshooting.md
            // rather than "fixed" here.
            if (!IsSalesforce(context.Registration)
                || context.GrantType is not OpenIddictConstants.GrantTypes.AuthorizationCode
                || context.TokenResponse is null)
            {
                return ValueTask.CompletedTask;
            }

            var instanceUrl = (string?)context.TokenResponse["instance_url"];
            if (!string.IsNullOrEmpty(instanceUrl))
            {
                context.Properties[OpenIddict.Constants.OAuthProperties.AccountLabel] = instanceUrl;
            }

            return ValueTask.CompletedTask;
        }
    }
}
