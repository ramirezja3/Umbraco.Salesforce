using System.Text.Json.Serialization;

namespace Automate.Salesforce.Connector.Api;

/// <summary>
/// Partial shape of Salesforce's <c>/services/oauth2/userinfo</c> response, used by the
/// connection types' <c>ValidateAsync</c> to confirm a token is live and report which organization/user
/// it's connected as.
/// </summary>
internal sealed class SalesforceUserInfoResponse
{
    [JsonPropertyName("organization_id")]
    public string? OrganizationId { get; set; }

    [JsonPropertyName("preferred_username")]
    public string? PreferredUsername { get; set; }
}
