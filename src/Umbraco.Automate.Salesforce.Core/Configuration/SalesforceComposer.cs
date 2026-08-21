using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.Extensions;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Connection;
using Umbraco.Automate.Salesforce.Persistence;
using Umbraco.Automate.Salesforce.Triggers;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Persistence.EFCore;
using Umbraco.Extensions;

namespace Umbraco.Automate.Salesforce.Configuration;

/// <summary>
/// Registers two Salesforce OAuth provider registrations with OpenIddict Client
/// WebIntegration — one for production (<c>login.salesforce.com</c>), one for sandbox
/// (<c>test.salesforce.com</c>) — plus this package's own services. Two registrations are
/// needed because a connection's OAuth issuer is fixed at startup per provider name; see
/// <see cref="Connection.SalesforceConnectionType"/> and docs/dev-notes.md §0a.
/// </summary>
public sealed class SalesforceComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<SalesforceApiOptions>(
            builder.Config.GetSection("Umbraco:Automate:Salesforce"));
        builder.Services.Configure<SalesforcePollingOptions>(
            builder.Config.GetSection("Umbraco:Automate:Salesforce:Polling"));

        builder.Services.AddSingleton<ISalesforceConnectionResolver, SalesforceConnectionResolver>();
        builder.Services.AddSingleton<ISalesforceClient, SalesforceClient>();
        builder.Services.AddSingleton<ISalesforceTriggerConnectionResolver, SalesforceTriggerConnectionResolver>();
        builder.Services.AddSingleton<ISalesforcePollingStateStore, SalesforcePollingStateStore>();
        builder.Services.AddHostedService<SalesforcePollingBackgroundJob>();

        // Resolved lazily inside the factory (run time), not here at composition time — same
        // reasoning as Umbraco.Automate.OpenIddict.Core.AddPersistence: hosts like Umbraco Cloud
        // synthesise the connection string through the ConnectionStrings options pipeline, which
        // hasn't run yet during AddComposers().
        builder.Services.AddUmbracoDbContext<SalesforceDbContext>(
            (IServiceProvider serviceProvider, DbContextOptionsBuilder options, string? _, string? _) =>
            {
                var (connectionString, providerName) = DatabaseConnectionInfo.Resolve(
                    serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>(),
                    serviceProvider.GetRequiredService<IConfiguration>());
                SalesforceDbContext.ConfigureProvider(options, connectionString, providerName);
            },
            shareUmbracoConnection: false);

        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunSalesforceMigrationNotificationHandler>();

        builder.Services.AddOpenIddict()
            .AddClient(options =>
            {
                // The "refresh_token" OAuth *scope* (added per-registration below) only makes
                // Salesforce issue a refresh token — OpenIddict's client separately needs the
                // refresh_token *grant type* allowed client-wide before it will ever redeem one
                // for a new access token. Confirmed via live testing: without this,
                // OAuthCredentialsService.RefreshAccessTokenAsync throws InvalidOperationException
                // ("has not been enabled in the OpenIddict client options") the moment the
                // short-lived access token expires. Per-registration AddGrantTypes (below) alone
                // is not sufficient — this client-wide switch is the one that actually matters.
                options.AllowRefreshTokenFlow();

                // Per-registration AddGrantTypes below must list BOTH authorization_code and
                // refresh_token, not just refresh_token. Confirmed by decompiling
                // OpenIddict.Client.WebIntegration: AddGrantTypes does registration.GrantTypes
                // .UnionWith(...) on a HashSet<string> that starts EMPTY on a freshly constructed
                // registration — it does not "add to an existing default set". Calling it with
                // only RefreshToken (as an earlier pass here did) makes GrantTypes = {"refresh_token"}
                // exclusively, which excludes authorization_code and breaks the initial "Authenticate"
                // challenge entirely with "A common grant type/response type combination... couldn't
                // be negotiated automatically" — a real regression, caught live, not a theoretical risk.

                options.UseWebProviders().AddSalesforce(salesforce =>
                {
                    salesforce.SetIssuer(new Uri("https://login.salesforce.com/"));
                    salesforce.AddScopes("api", "refresh_token");
                    salesforce.AddGrantTypes(OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.RefreshToken);
                });

                options.UseWebProviders().AddSalesforce(salesforce =>
                {
                    salesforce.SetProviderName("SalesforceSandbox");
                    salesforce.SetRegistrationId("SalesforceSandbox");
                    salesforce.SetIssuer(new Uri("https://test.salesforce.com/"));
                    salesforce.AddScopes("api", "refresh_token");
                    salesforce.AddGrantTypes(OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.RefreshToken);
                });

                foreach (var descriptor in SalesforceOAuthHandlers.Descriptors)
                {
                    options.AddEventHandler(descriptor);
                }
            });
    }
}
