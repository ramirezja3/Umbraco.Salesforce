namespace Umbraco.Automate.Salesforce.Connection;

/// <summary>
/// A resolved, ready-to-use Salesforce API credential: a valid access token plus the organization's
/// instance URL (the actual API host — distinct from the login host used for authentication;
/// see <see cref="ISalesforceConnectionResolver"/>). <paramref name="CredentialsId"/> is carried
/// along so <see cref="Api.SalesforceClient"/> can force a token refresh and retry once if
/// Salesforce reports the session invalid mid-call (see <see cref="ISalesforceConnectionResolver.ForceRefreshAsync"/>).
/// </summary>
public sealed record SalesforceConnectionContext(Guid CredentialsId, string AccessToken, Uri InstanceUrl)
{
    /// <summary>
    /// Redacts <see cref="AccessToken"/> from the record's default printed form — records
    /// auto-generate a <c>ToString()</c> that includes every property, which would otherwise put
    /// a live access token into any log line or exception message that ever interpolates this
    /// record as a whole (e.g. <c>$"{connection}"</c>) instead of a specific field.
    /// </summary>
    public override string ToString() =>
        $"SalesforceConnectionContext {{ CredentialsId = {CredentialsId}, AccessToken = [redacted], InstanceUrl = {InstanceUrl} }}";
}
