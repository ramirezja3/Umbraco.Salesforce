using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Api;

/// <inheritdoc cref="ISalesforceClient"/>
internal sealed class SalesforceClient : ISalesforceClient
{
    private const string RateLimitErrorCode = "REQUEST_LIMIT_EXCEEDED";
    private const string InvalidSessionErrorCode = "INVALID_SESSION_ID";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ILogger<SalesforceClient> _logger;

    public SalesforceClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<SalesforceApiOptions> options,
        ISalesforceConnectionResolver connectionResolver,
        ILogger<SalesforceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _connectionResolver = connectionResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SalesforceApiResult> SendAsync(
        SalesforceConnectionContext connection,
        HttpMethod method,
        string relativePath,
        object? jsonBody,
        CancellationToken cancellationToken)
    {
        // +1: MaxRetryAttempts means retries *after* the first attempt, matching its doc comment —
        // a value of 3 allows 4 total attempts, not 3. Found live during QA: the previous
        // `Math.Max(1, MaxRetryAttempts)` treated the configured value as the total attempt count,
        // silently granting one fewer retry than documented.
        var maxAttempts = Math.Max(0, _options.CurrentValue.MaxRetryAttempts) + 1;
        var sessionRefreshUsed = false;

        for (var attempt = 1; ; attempt++)
        {
            var requestUri = new Uri(connection.InstanceUrl, relativePath);

            using var client = _httpClientFactory.CreateClient("UmbracoAutomate");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);

            using var request = new HttpRequestMessage(method, requestUri);
            if (jsonBody is not null)
            {
                request.Content = JsonContent.Create(jsonBody);
            }

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                && !cancellationToken.IsCancellationRequested)
            {
                // A DNS failure, connection refused, TLS handshake error, or timeout — Salesforce
                // never got a chance to return a response for SalesforceErrorMapper.Map to
                // categorize. Previously this propagated as a raw HttpRequestException/
                // TaskCanceledException straight to the Run log. `!cancellationToken
                // .IsCancellationRequested` excludes the caller's own cancellation (e.g. the
                // automation run being stopped) from this mapping — that should still propagate
                // as a cancellation, not a "network unreachable" error.
                _logger.LogWarning(ex,
                    "Network-level failure calling Salesforce ({InstanceUrl}) for request {Method} {Path}.",
                    connection.InstanceUrl, method, relativePath);
                return new SalesforceApiResult
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    IsSuccess = false,
                    Error = SalesforceErrorMapper.MapException(ex, connection.InstanceUrl),
                };
            }

            using var responseScope = response;
            var rawBody = response.Content.Headers.ContentLength is 0
                ? null
                : await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                JsonElement? json = null;
                if (!string.IsNullOrWhiteSpace(rawBody))
                {
                    // Clone() copies the element into its own independent buffer, so the document
                    // can be disposed immediately afterward, returning its rented parse buffer to
                    // the shared array pool instead of holding it for the lifetime of the result.
                    using var document = JsonDocument.Parse(rawBody);
                    json = document.RootElement.Clone();
                }

                return new SalesforceApiResult { StatusCode = response.StatusCode, IsSuccess = true, Json = json };
            }

            var error = SalesforceErrorMapper.Map(response.StatusCode, rawBody);

            // Salesforce's Web Server OAuth flow doesn't return expires_in, so the locally
            // tracked token expiry never trips and this package never proactively refreshes
            // (see ISalesforceConnectionResolver.ForceRefreshAsync). The only signal that the
            // session went stale — organization session timeout, revocation, IP-restriction change — is
            // Salesforce rejecting a call with INVALID_SESSION_ID. Recover once by forcing a
            // refresh and retrying with the new token, rather than failing every run until an
            // implementer manually reconnects.
            if (error.ErrorCode == InvalidSessionErrorCode && !sessionRefreshUsed)
            {
                sessionRefreshUsed = true;
                _logger.LogWarning(
                    "Salesforce session invalid for request {Method} {Path} — forcing a token refresh and retrying once.",
                    method, relativePath);

                var refreshed = await _connectionResolver.ForceRefreshAsync(connection.CredentialsId, cancellationToken);
                if (refreshed is not null)
                {
                    connection = refreshed;
                    continue;
                }

                _logger.LogWarning(
                    "Salesforce session refresh failed for credentials {CredentialsId} — the connection needs to be reconnected.",
                    connection.CredentialsId);
            }

            var isRateLimited = response.StatusCode == HttpStatusCode.TooManyRequests
                || error.ErrorCode == RateLimitErrorCode;

            if (!isRateLimited || attempt >= maxAttempts)
            {
                return new SalesforceApiResult { StatusCode = response.StatusCode, IsSuccess = false, Error = error };
            }

            var delay = ComputeRetryDelay(response.Headers.RetryAfter, attempt, _options.CurrentValue.MaxRetryDelay);
            _logger.LogWarning(
                "Salesforce rate-limited request {Method} {Path} (attempt {Attempt}/{MaxAttempts}) — retrying in {Delay}.",
                method, relativePath, attempt, maxAttempts, delay);
            await Task.Delay(delay, cancellationToken);
        }
    }

    /// <summary>
    /// Honors Salesforce's <c>Retry-After</c> header when present; otherwise falls back to
    /// exponential backoff with jitter (docs/dev-notes.md §2 non-negotiable #7, §8). Either way,
    /// the result is capped to <paramref name="maxDelay"/> — uncapped exponential growth could
    /// otherwise block a workflow step for minutes on a high <c>MaxRetryAttempts</c>, and
    /// Salesforce's own <c>Retry-After</c> value is honored but not blindly trusted past a
    /// sensible ceiling either.
    /// </summary>
    private static TimeSpan ComputeRetryDelay(RetryConditionHeaderValue? retryAfter, int attempt, TimeSpan maxDelay)
    {
        if (retryAfter?.Delta is { } delta)
        {
            return Min(delta, maxDelay);
        }

        if (retryAfter?.Date is { } date)
        {
            var untilDate = date - DateTimeOffset.UtcNow;
            if (untilDate > TimeSpan.Zero)
            {
                return Min(untilDate, maxDelay);
            }
        }

        var baseDelayMs = Math.Pow(2, attempt) * 500;
        var jitterMs = Random.Shared.Next(0, 250);
        return Min(TimeSpan.FromMilliseconds(baseDelayMs + jitterMs), maxDelay);
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
