using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Automate.Salesforce.Connector.Api;
using Automate.Salesforce.Connector.Configuration;
using Automate.Salesforce.Connector.Connection;

namespace Automate.Salesforce.Connector.Tests.Integration.LiveSalesforce;

/// <summary>
/// xUnit fixture that authenticates against a real Salesforce org via the OAuth 2.0 Client
/// Credentials flow (server-to-server, no interactive login) and exposes a real
/// <see cref="ISalesforceClient"/> plus a resolved <see cref="SalesforceConnectionContext"/> for
/// tests to call this package's actual production code against.
/// </summary>
/// <remarks>
/// This validates the REST data-path code (<see cref="SalesforceClient"/>, actions, polling
/// triggers) against a live org. It deliberately does <em>not</em> exercise the interactive
/// Authorization Code + PKCE flow the real connection types use — that requires a browser and a
/// running Umbraco backoffice, which this test project has neither. Client Credentials Flow only
/// needs to be enabled on the Connected App for local testing; it isn't part of the shipped
/// product.
/// </remarks>
public sealed class LiveSalesforceFixture : IAsyncLifetime
{
    /// <summary>Gets the loaded credentials, or <c>null</c> if none are configured locally.</summary>
    public LiveSalesforceCredentials? Credentials { get; private set; }

    /// <summary>Gets a resolved connection context, once <see cref="InitializeAsync"/> has run. Null if not configured.</summary>
    public SalesforceConnectionContext? Connection { get; private set; }

    /// <summary>Gets a real <see cref="ISalesforceClient"/> wired against the live org.</summary>
    public ISalesforceClient Client { get; private set; } = null!;

    /// <summary>Gets the resolved API options (mirrors what the composer would bind from config).</summary>
    public SalesforceApiOptions ApiOptions { get; private set; } = new();

    public async Task InitializeAsync()
    {
        Credentials = LiveSalesforceCredentials.TryLoad();
        if (Credentials is null)
        {
            return;
        }

        ApiOptions = new SalesforceApiOptions { ApiVersion = Credentials.ApiVersion };

        using var http = new HttpClient();
        var tokenResponse = await http.PostAsync(
            $"{Credentials.MyDomainUrl}/services/oauth2/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = Credentials.ClientId,
                ["client_secret"] = Credentials.ClientSecret,
            }));

        var body = await tokenResponse.Content.ReadAsStringAsync();
        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Live Salesforce credentials are configured but the Client Credentials Flow token request failed " +
                $"(HTTP {(int)tokenResponse.StatusCode}). Check that the flow is enabled on the Connected App. Body: {body}");
        }

        var json = JsonDocument.Parse(body).RootElement;
        var accessToken = json.GetProperty("access_token").GetString()!;
        var instanceUrl = json.TryGetProperty("instance_url", out var iu) ? iu.GetString()! : Credentials.MyDomainUrl;

        // No stored OAuthCredentials backs this Client Credentials Flow token, so there's nothing
        // for ISalesforceConnectionResolver.ForceRefreshAsync to refresh — Guid.Empty is fine,
        // since these tests never provoke an INVALID_SESSION_ID mid-call.
        Connection = new SalesforceConnectionContext(Guid.Empty, accessToken, new Uri(instanceUrl));

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() =>
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        });

        var options = new Mock<IOptionsMonitor<SalesforceApiOptions>>();
        options.Setup(o => o.CurrentValue).Returns(ApiOptions);

        Client = new SalesforceClient(
            httpClientFactory.Object, options.Object, Mock.Of<ISalesforceConnectionResolver>(), NullLogger<SalesforceClient>.Instance);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
