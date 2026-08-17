namespace Umbraco.Automate.Salesforce.Connection;

/// <summary>
/// A resolved, ready-to-use Salesforce API credential: a valid access token plus the org's
/// instance URL (the actual API host — distinct from the login host used for authentication;
/// see <see cref="ISalesforceConnectionResolver"/>).
/// </summary>
public sealed record SalesforceConnectionContext(string AccessToken, Uri InstanceUrl);
