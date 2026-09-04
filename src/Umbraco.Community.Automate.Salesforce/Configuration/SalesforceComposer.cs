using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Umbraco.Community.Automate.Salesforce.Api;
using Umbraco.Community.Automate.Salesforce.Connection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.Automate.Salesforce.Configuration;

/// <summary>
/// Registers the Salesforce OAuth provider registration with OpenIddict Client WebIntegration
/// (<c>login.salesforce.com</c>) plus this package's own services.
/// </summary>
public sealed class SalesforceComposer : IComposer
{
    // Requesting only what's actually needed keeps this least-privilege — an implementer who
    // needs a broader scope for their own use of the connection adds it under
    // Umbraco:Automate:Providers:Salesforce:Scopes rather than this package silently ignoring it.
    private static readonly string[] DefaultScopes = ["api", "refresh_token"];

    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<SalesforceApiOptions>(
            builder.Config.GetSection("Umbraco:Automate:Salesforce"));

        builder.Services.AddSingleton<ISalesforceConnectionResolver, SalesforceConnectionResolver>();
        builder.Services.AddSingleton<ISalesforceClient, SalesforceClient>();

        builder.Services.AddOpenIddict()
            .AddClient(options =>
            {
                // The "refresh_token" OAuth *scope* (added per-registration below) only makes
                // Salesforce issue a refresh token — OpenIddict's client separately needs the
                // refresh_token *grant type* allowed client-wide before it will ever redeem one
                // for a new access token. Per-registration AddGrantTypes (below) alone is not
                // sufficient — this client-wide switch is the one that actually matters.
                options.AllowRefreshTokenFlow();

                // Per-registration AddGrantTypes below must list BOTH authorization_code and
                // refresh_token, not just refresh_token: AddGrantTypes does
                // registration.GrantTypes.UnionWith(...) on a HashSet<string> that starts EMPTY on
                // a freshly constructed registration, rather than adding to an existing default
                // set. Passing only RefreshToken would make GrantTypes = {"refresh_token"}
                // exclusively, excluding authorization_code and breaking the initial "Authenticate"
                // challenge.

                options.UseWebProviders().AddSalesforce(salesforce =>
                {
                    salesforce.SetIssuer(new Uri("https://login.salesforce.com/"));
                    salesforce.AddScopes(ResolveScopes(builder.Config, "Salesforce"));
                    salesforce.AddGrantTypes(OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.RefreshToken);
                });

                foreach (var descriptor in SalesforceOAuthHandlers.Descriptors)
                {
                    options.AddEventHandler(descriptor);
                }
            });
    }

    /// <summary>
    /// Reads <c>Umbraco:Automate:Providers:{providerName}:Scopes</c> — the same config shape
    /// <c>Umbraco.Automate.OpenIddict</c>'s <c>OAuthProviderConfiguration</c> documents for every
    /// provider — falling back to <see cref="DefaultScopes"/> when unset or empty.
    /// </summary>
    internal static string[] ResolveScopes(IConfiguration config, string providerName)
    {
        var configured = config.GetSection($"Umbraco:Automate:Providers:{providerName}:Scopes").Get<string[]>();
        return configured is { Length: > 0 } ? configured : DefaultScopes;
    }
}
