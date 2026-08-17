using Microsoft.Extensions.DependencyInjection;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Connection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

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

        builder.Services.AddSingleton<ISalesforceConnectionResolver, SalesforceConnectionResolver>();
        builder.Services.AddSingleton<ISalesforceClient, SalesforceClient>();

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
