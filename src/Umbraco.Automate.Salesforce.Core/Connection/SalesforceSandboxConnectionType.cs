using System.Net.Http.Headers;
using System.Net.Http.Json;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.OpenIddict.ConnectionTypes;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Automate.Salesforce.Api;

namespace Umbraco.Automate.Salesforce.Connection;

/// <summary>
/// Connection type for a Salesforce sandbox org (authenticates against
/// <c>test.salesforce.com</c>). See <see cref="SalesforceConnectionType"/> for the production
/// equivalent and why these are two separate connection types.
/// </summary>
[ConnectionType("salesforce-sandbox", "Salesforce (Sandbox)", Group = "CRM", Icon = "icon-cloud", Description = "Connect to a Salesforce sandbox org")]
public sealed class SalesforceSandboxConnectionType : OAuthConnectionTypeBase<SalesforceSandboxConnectionSettings>
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SalesforceSandboxConnectionType"/> class.
    /// </summary>
    public SalesforceSandboxConnectionType(
        ConnectionTypeInfrastructure infrastructure,
        IOAuthCredentialsService credentialsService,
        IHttpClientFactory httpClientFactory)
        : base(infrastructure, credentialsService)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public override string ProviderName => "SalesforceSandbox";

    /// <inheritdoc />
    public override string? SetupDocsUrl =>
        "https://help.salesforce.com/s/articleView?id=sf.connected_app_create.htm";

    /// <inheritdoc cref="SalesforceConnectionType.ValidateAsync"/>
    public override async Task<ConnectionValidationResult> ValidateAsync(
        object? settings,
        CancellationToken cancellationToken)
    {
        var baseResult = await base.ValidateAsync(settings, cancellationToken);
        if (baseResult.Status != ConnectionValidationStatus.Success)
        {
            return baseResult;
        }

        var credentialsId = ((SalesforceSandboxConnectionSettings)settings!).OAuthCredentialsId!.Value;
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

        var org = response?.OrganizationId ?? "your Salesforce sandbox";
        var user = response?.PreferredUsername is null ? string.Empty : $" as {response.PreferredUsername}";
        return ConnectionValidationResult.Success($"Connected to {org}{user} ({instanceUrl.Host}).");
    }
}
