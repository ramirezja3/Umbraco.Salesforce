using System.Text.Json;
using Umbraco.Automate.Core.Actions;

namespace Automate.Salesforce.Connector.Api;

/// <summary>
/// Maps a Salesforce REST API error response into a human-readable message and
/// <see cref="StepRunErrorCategory"/>, so the automation Run log shows something an implementer
/// can act on instead of a raw JSON dump (docs/dev-notes.md §7/§8). See also
/// docs/troubleshooting.md for the full error-code reference this mirrors.
/// </summary>
public static class SalesforceErrorMapper
{
    /// <summary>
    /// Parses a Salesforce error response body (a JSON array of <c>{ message, errorCode, fields }</c>
    /// objects for standard REST API errors, or a single <c>{ error, error_description }</c> object
    /// for OAuth-layer errors) into a mapped <see cref="SalesforceApiError"/>.
    /// </summary>
    public static SalesforceApiError Map(System.Net.HttpStatusCode statusCode, string? rawBody)
    {
        var (message, errorCode) = ExtractFirstError(rawBody);
        var category = Categorize(statusCode, errorCode);
        return new SalesforceApiError(Humanize(errorCode, message, statusCode), errorCode, category);
    }

    /// <summary>
    /// Maps a network-level exception (DNS failure, connection refused, TLS handshake error,
    /// timeout) thrown by the underlying <see cref="HttpClient"/> itself — before Salesforce ever
    /// returned a response for <see cref="Map"/> to categorize — into the same
    /// <see cref="SalesforceApiError"/> shape, so it surfaces in the Run log as something an
    /// implementer can act on instead of a raw .NET exception.
    /// </summary>
    public static SalesforceApiError MapException(Exception exception, Uri instanceUrl) =>
        new(
            $"Could not reach Salesforce ({instanceUrl.Host}) — check network connectivity. ({exception.Message})",
            ErrorCode: null,
            StepRunErrorCategory.ServiceUnavailable);

    private static (string? Message, string? ErrorCode) ExtractFirstError(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return (null, null);
        }

        // Salesforce's identity endpoints (/services/oauth2/userinfo, /id/...) — unlike the REST
        // Data API — return a bare plain-text token on failure, not JSON, e.g. "Bad_OAuth_Token"
        // for a stale/invalid access token. Confirmed live: the exact same underlying condition
        // (an access token Salesforce no longer accepts) surfaces as this plain-text string here
        // but as a JSON INVALID_SESSION_ID error from the REST Data API. Map it onto the same
        // code so both the retry-with-refresh logic in SalesforceClient and the humanized message
        // below treat the two shapes identically instead of only reacting to the JSON one.
        if (rawBody.Trim().Equals("Bad_OAuth_Token", StringComparison.OrdinalIgnoreCase))
        {
            return ("The Salesforce session is no longer valid.", "INVALID_SESSION_ID");
        }

        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;

            // Standard REST data API shape: a JSON array of error objects. Every shipped action
            // calls the /sobjects/... REST Data API exclusively, which only ever produces this
            // shape — there is no Invocable Actions REST resource call anywhere in this package's
            // six actions to produce a different one.
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var first = root[0];
                var message = first.TryGetProperty("message", out var m) ? m.GetString() : null;
                var errorCode = first.TryGetProperty("errorCode", out var c) ? c.GetString() : null;
                return (message, errorCode);
            }

            // OAuth-layer error shape: a single object with "error"/"error_description".
            if (root.ValueKind == JsonValueKind.Object)
            {
                var errorCode = root.TryGetProperty("error", out var e) ? e.GetString() : null;
                var message = root.TryGetProperty("error_description", out var d) ? d.GetString() : null;
                return (message, errorCode?.ToUpperInvariant());
            }
        }
        catch (JsonException)
        {
            // Not JSON at all (e.g. an HTML error page from a misconfigured instance URL) —
            // fall through and let the caller fall back to the raw body/status code.
        }

        return (null, null);
    }

    private static StepRunErrorCategory Categorize(System.Net.HttpStatusCode statusCode, string? errorCode) => errorCode switch
    {
        "REQUEST_LIMIT_EXCEEDED" => StepRunErrorCategory.RateLimiting,
        "INVALID_SESSION_ID" or "INVALID_GRANT" or "invalid_grant" => StepRunErrorCategory.Authentication,
        "INSUFFICIENT_ACCESS_OR_READONLY" or "INSUFFICIENT_ACCESS" => StepRunErrorCategory.Authentication,
        "INVALID_FIELD" or "REQUIRED_FIELD_MISSING" or "FIELD_CUSTOM_VALIDATION_EXCEPTION"
            or "STRING_TOO_LONG" or "MALFORMED_ID" or "INVALID_TYPE" => StepRunErrorCategory.Validation,
        "DUPLICATE_VALUE" or "DUPLICATES_DETECTED" => StepRunErrorCategory.Validation,
        "NOT_FOUND" => StepRunErrorCategory.InvalidResponse,
        _ => (int)statusCode switch
        {
            401 => StepRunErrorCategory.Authentication,
            403 => StepRunErrorCategory.Authentication,
            404 => StepRunErrorCategory.InvalidResponse,
            429 => StepRunErrorCategory.RateLimiting,
            >= 500 => StepRunErrorCategory.ServiceUnavailable,
            _ => StepRunErrorCategory.Unknown,
        },
    };

    private static string Humanize(string? errorCode, string? message, System.Net.HttpStatusCode statusCode)
    {
        if (errorCode is not null && message is not null)
        {
            return errorCode switch
            {
                "REQUEST_LIMIT_EXCEEDED" =>
                    "Salesforce API request limit exceeded for this organization. The action will retry with backoff; " +
                    "if this keeps happening, the organization's daily API allocation may be exhausted.",
                "INVALID_SESSION_ID" =>
                    "The Salesforce session is no longer valid. Reconnect the Salesforce connection.",
                "INSUFFICIENT_ACCESS_OR_READONLY" =>
                    $"The connected Salesforce user doesn't have access to perform this operation: {message}",
                "DUPLICATE_VALUE" =>
                    $"Salesforce rejected this as a duplicate: {message}",
                "DUPLICATES_DETECTED" =>
                    "Salesforce's Duplicate Rules blocked this write because a similar record already exists " +
                    "(e.g. a Contact and a Lead sharing the same email). Either update the existing record instead, " +
                    "or adjust the organization's Duplicate Rule for this object in Salesforce Setup.",
                "REQUIRED_FIELD_MISSING" =>
                    $"A required Salesforce field is missing: {message}",
                "FIELD_CUSTOM_VALIDATION_EXCEPTION" =>
                    $"Salesforce validation rule failed: {message}",
                _ => $"Salesforce returned {errorCode}: {message}",
            };
        }

        if (message is not null)
        {
            return message;
        }

        return $"Salesforce API call failed with HTTP {(int)statusCode}.";
    }
}
