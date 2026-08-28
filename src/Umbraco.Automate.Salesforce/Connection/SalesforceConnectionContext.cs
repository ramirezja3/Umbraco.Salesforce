namespace Umbraco.Automate.Salesforce.Connection;

/// <summary>
/// A resolved, ready-to-use Salesforce API credential: a valid access token plus the organization's
/// instance URL (the actual API host — distinct from the login host used for authentication;
/// see <see cref="ISalesforceConnectionResolver"/>). <paramref name="CredentialsId"/> is carried
/// along so <see cref="Api.SalesforceClient"/> can force a token refresh and retry once if
/// Salesforce reports the session invalid mid-call (see <see cref="ISalesforceConnectionResolver.ForceRefreshAsync"/>).
/// </summary>
public sealed record SalesforceConnectionContext(Guid CredentialsId, string AccessToken, Uri InstanceUrl);
