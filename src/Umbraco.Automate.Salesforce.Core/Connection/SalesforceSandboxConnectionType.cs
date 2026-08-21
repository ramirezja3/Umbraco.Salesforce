using System.Text.Json;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.OpenIddict.ConnectionTypes;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Automate.Salesforce.Api;

namespace Umbraco.Automate.Salesforce.Connection;

/// <summary>
/// Connection type for a Salesforce sandbox organization (authenticates against
/// <c>test.salesforce.com</c>). See <see cref="SalesforceConnectionType"/> for the production
/// equivalent and why these are two separate connection types.
/// </summary>
[ConnectionType("salesforce-sandbox", "Salesforce (Sandbox)", Group = "Salesforce", Icon = "icon-cloud", Description = "Connect to a Salesforce sandbox organization")]
public sealed class SalesforceSandboxConnectionType : OAuthConnectionTypeBase<SalesforceSandboxConnectionSettings>
{
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="SalesforceSandboxConnectionType"/> class.
    /// </summary>
    public SalesforceSandboxConnectionType(
        ConnectionTypeInfrastructure infrastructure,
        IOAuthCredentialsService credentialsService,
        ISalesforceConnectionResolver connectionResolver,
        ISalesforceClient client)
        : base(infrastructure, credentialsService)
    {
        _connectionResolver = connectionResolver;
        _client = client;
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
        var connection = await _connectionResolver.ResolveAsync(credentialsId, cancellationToken);
        if (connection is null)
        {
            return ConnectionValidationResult.Failure(
                "The Salesforce access token is expired or revoked, or no instance URL was captured for this connection. Reconnect the account.");
        }

        SalesforceApiResult result;
        try
        {
            result = await _client.SendAsync(connection, HttpMethod.Get, "/services/oauth2/userinfo", null, cancellationToken);
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

        if (!result.IsSuccess)
        {
            return ConnectionValidationResult.Failure(result.Error!.Message);
        }

        var response = result.Json is { } json ? JsonSerializer.Deserialize<SalesforceUserInfoResponse>(json) : null;
        var org = response?.OrganizationId ?? "your Salesforce sandbox";
        var user = response?.PreferredUsername is null ? string.Empty : $" as {response.PreferredUsername}";
        return ConnectionValidationResult.Success($"Connected to {org}{user} ({connection.InstanceUrl.Host}).");
    }
}
