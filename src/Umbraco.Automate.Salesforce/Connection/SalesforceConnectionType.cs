using System.Text.Json;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.OpenIddict.ConnectionTypes;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Automate.Salesforce.Api;

namespace Umbraco.Automate.Salesforce.Connection;

/// <summary>
/// Connection type for a Salesforce organization (authenticates against
/// <c>login.salesforce.com</c>), using OAuth via OpenIddict WebIntegration.
/// </summary>
[ConnectionType("salesforce", "Salesforce", Group = "Salesforce", Icon = "icon-cloud", Description = "Connect to a Salesforce organization")]
public sealed class SalesforceConnectionType : OAuthConnectionTypeBase<SalesforceConnectionSettings>
{
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="SalesforceConnectionType"/> class.
    /// </summary>
    public SalesforceConnectionType(
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
    public override string ProviderName => "Salesforce";

    /// <inheritdoc />
    public override string? SetupDocsUrl =>
        "https://help.salesforce.com/s/articleView?id=sf.connected_app_create.htm";

    /// <summary>
    /// Adds a Salesforce-specific check on top of the base token-resolution check: calls the
    /// organization's <c>userinfo</c> endpoint to confirm the token actually works and to report which
    /// organization/user it's connected as. Mirrors <c>Umbraco.Automate.Slack</c>'s <c>auth.test</c> check.
    /// Routed through <see cref="ISalesforceClient"/> (rather than a separate hand-rolled HTTP
    /// call) deliberately — confirmed live that a stale access token is otherwise indistinguishable
    /// from a genuine 403 here, whereas going through the shared client gets the same
    /// force-refresh-and-retry-once recovery every action/trigger already benefits from
    /// (see <see cref="SalesforceClient"/> and <see cref="SalesforceErrorMapper"/>'s handling of
    /// the identity endpoint's plain-text <c>Bad_OAuth_Token</c> error).
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
        var org = response?.OrganizationId ?? "your Salesforce org";
        var user = response?.PreferredUsername is null ? string.Empty : $" as {response.PreferredUsername}";
        return ConnectionValidationResult.Success($"Connected to {org}{user} ({connection.InstanceUrl.Host}).");
    }
}
