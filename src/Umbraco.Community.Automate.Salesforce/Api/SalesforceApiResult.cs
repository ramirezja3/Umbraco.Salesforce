using System.Net;
using System.Text.Json;

namespace Umbraco.Community.Automate.Salesforce.Api;

/// <summary>
/// The outcome of a single Salesforce REST API call — see <see cref="ISalesforceClient"/>.
/// </summary>
public sealed class SalesforceApiResult
{
    /// <summary>Gets the HTTP status code returned by Salesforce.</summary>
    public required HttpStatusCode StatusCode { get; init; }

    /// <summary>Gets a value indicating whether the call succeeded (2xx).</summary>
    public required bool IsSuccess { get; init; }

    /// <summary>Gets the parsed JSON response body, when present and successful.</summary>
    public JsonElement? Json { get; init; }

    /// <summary>Gets the mapped error, populated when <see cref="IsSuccess"/> is <c>false</c>.</summary>
    public SalesforceApiError? Error { get; init; }
}
