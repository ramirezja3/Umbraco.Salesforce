using Automate.Salesforce.Connector.Connection;

namespace Automate.Salesforce.Connector.Api;

/// <summary>
/// Executes calls against the Salesforce REST API (<c>/services/data/vXX.X/...</c>), applying
/// backoff/retry on rate-limit responses and mapping errors into readable messages (docs/dev-notes.md
/// §2 non-negotiable #7, §8). Actions resolve a <see cref="SalesforceConnectionContext"/> via
/// <see cref="ISalesforceConnectionResolver"/> first, then call this.
/// </summary>
public interface ISalesforceClient
{
    /// <summary>
    /// Sends a request to the Salesforce REST API.
    /// </summary>
    /// <param name="connection">The resolved access token and instance URL to call.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="relativePath">
    /// The path relative to the instance URL, e.g. <c>/services/data/v62.0/sobjects/Lead</c>.
    /// The API version segment is the caller's responsibility — see <c>SalesforceApiOptions.ApiVersion</c>.
    /// </param>
    /// <param name="jsonBody">The request body to serialize as JSON, or <c>null</c> for none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<SalesforceApiResult> SendAsync(
        SalesforceConnectionContext connection,
        HttpMethod method,
        string relativePath,
        object? jsonBody,
        CancellationToken cancellationToken);
}
