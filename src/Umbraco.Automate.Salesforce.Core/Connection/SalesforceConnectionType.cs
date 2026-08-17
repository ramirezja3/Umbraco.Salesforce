using System.Net.Http.Headers;
using System.Net.Http.Json;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.OpenIddict.ConnectionTypes;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Automate.Salesforce.Api;

namespace Umbraco.Automate.Salesforce.Connection;

/// <summary>
/// Connection type for a production Salesforce org (authenticates against
/// <c>login.salesforce.com</c>), using OAuth via OpenIddict WebIntegration.
/// </summary>
/// <remarks>
/// See <see cref="SalesforceSandboxConnectionType"/> for the sandbox equivalent. These are two
/// separate connection types — not one type with an environment field — because OpenIddict
/// Client registrations (and therefore each one's authorization/token endpoint) are fixed at
/// startup from configuration; a single connection type has no way to redirect its OAuth
/// challenge to a different issuer per connection instance. See CLAUDE.md §0a.
/// </remarks>
[ConnectionType("salesforce", "Salesforce", Group = "CRM", Icon = "icon-cloud", Description = "Connect to a Salesforce production org")]
public sealed class SalesforceConnectionType : OAuthConnectionTypeBase<SalesforceConnectionSettings>
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SalesforceConnectionType"/> class.
    /// </summary>
    public SalesforceConnectionType(
        ConnectionTypeInfrastructure infrastructure,
        IOAuthCredentialsService credentialsService,
        IHttpClientFactory httpClientFactory)
        : base(infrastructure, credentialsService)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public override string ProviderName => "Salesforce";

    /// <inheritdoc />
    public override string? SetupDocsUrl =>
        "https://help.salesforce.com/s/articleView?id=sf.connected_app_create.htm";

    /// <summary>
    /// Adds a Salesforce-specific check on top of the base token-resolution check: calls the
    /// org's <c>userinfo</c> endpoint to confirm the token actually works and to report which
    /// org/user it's connected as. Mirrors <c>Umbraco.Automate.Slack</c>'s <c>auth.test</c> check.
    /// </summary>
    public override async Task<ConnectionValidationResult> ValidateAsync(
        object? settings,
        CancellationToken cancellationToken)
    {
        var baseResult = await base.ValidateAsync(settings, cancellationToken);
        if (baseResult.Status != ConnectionValidationStatus.Success)
        {
            return baseResult;
        }

        var credentialsId = ((SalesforceConnectionSettings)settings!).OAuthCredentialsId!.Value;
        var token = await CredentialsService.GetValidAccessTokenAsync(credentialsId, cancellationToken);
        var credentials = await CredentialsService.GetCredentialsAsync(credentialsId, cancellationToken);

        if (string.IsNullOrEmpty(credentials?.AccountLabel)
            || !Uri.TryCreate(credentials.AccountLabel, UriKind.Absolute, out var instanceUrl))
        {
            return ConnectionValidationResult.Failure(
                "No instance URL was captured for this connection. Reconnect the account.");
        }

        using var client = _httpClientFactory.CreateClient("UmbracoAutomate");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        SalesforceUserInfoResponse? response;
        try
        {
            using var httpResponse = await client.GetAsync(
                new Uri(instanceUrl, "/services/oauth2/userinfo"), cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                return ConnectionValidationResult.Failure(
                    $"Salesforce rejected the access token (HTTP {(int)httpResponse.StatusCode}).");
            }

            response = await httpResponse.Content.ReadFromJsonAsync<SalesforceUserInfoResponse>(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ConnectionValidationResult.Failure(
                "Could not reach the Salesforce API.",
                [ex.Message]);
        }

        var org = response?.OrganizationId ?? "your Salesforce org";
        var user = response?.PreferredUsername is null ? string.Empty : $" as {response.PreferredUsername}";
        return ConnectionValidationResult.Success($"Connected to {org}{user} ({instanceUrl.Host}).");
    }
}
