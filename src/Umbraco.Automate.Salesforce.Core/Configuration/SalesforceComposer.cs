using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
/// <see cref="Connection.SalesforceConnectionType"/> and CLAUDE.md §0a.
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
                options.UseWebProviders().AddSalesforce(salesforce =>
                {
                    salesforce.SetIssuer(new Uri("https://login.salesforce.com/"));
                });

                options.UseWebProviders().AddSalesforce(salesforce =>
                {
                    salesforce.SetProviderName("SalesforceSandbox");
                    salesforce.SetRegistrationId("SalesforceSandbox");
                    salesforce.SetIssuer(new Uri("https://test.salesforce.com/"));
                });

                foreach (var descriptor in SalesforceOAuthHandlers.Descriptors)
                {
                    options.AddEventHandler(descriptor);
                }
            });
    }
}
