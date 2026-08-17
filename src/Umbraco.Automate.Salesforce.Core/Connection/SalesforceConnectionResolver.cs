using Umbraco.Automate.OpenIddict.Credentials;

namespace Umbraco.Automate.Salesforce.Connection;

/// <inheritdoc cref="ISalesforceConnectionResolver"/>
internal sealed class SalesforceConnectionResolver : ISalesforceConnectionResolver
{
    private readonly IOAuthCredentialsService _credentialsService;

    public SalesforceConnectionResolver(IOAuthCredentialsService credentialsService)
    {
        _credentialsService = credentialsService;
    }

    /// <inheritdoc />
    public async Task<SalesforceConnectionContext?> ResolveAsync(Guid credentialsId, CancellationToken cancellationToken)
    {
        var accessToken = await _credentialsService.GetValidAccessTokenAsync(credentialsId, cancellationToken);
        if (string.IsNullOrEmpty(accessToken))
        {
            return null;
        }

        var credentials = await _credentialsService.GetCredentialsAsync(credentialsId, cancellationToken);

        // The instance URL is captured from the token response at authentication time and
        // stashed in the generic AccountLabel column — see SalesforceOAuthHandlers.ExtractInstanceUrl.
        // There is no Salesforce-specific persistence for this: AccountLabel already exists on
        // Umbraco.Automate.OpenIddict's credential entity and needs no schema change to reuse.
        if (string.IsNullOrEmpty(credentials?.AccountLabel)
            || !Uri.TryCreate(credentials.AccountLabel, UriKind.Absolute, out var instanceUrl))
        {
            return null;
        }

        return new SalesforceConnectionContext(accessToken, instanceUrl);
    }
}
