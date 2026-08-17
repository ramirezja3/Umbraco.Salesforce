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
    private const string SalesforceErrorCode = "REQUEST_LIMIT_EXCEEDED";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;
    private readonly ILogger<SalesforceClient> _logger;

    public SalesforceClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<SalesforceApiOptions> options,
        ILogger<SalesforceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
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
        var maxAttempts = Math.Max(1, _options.CurrentValue.MaxRetryAttempts);
        var requestUri = new Uri(connection.InstanceUrl, relativePath);

        for (var attempt = 1; ; attempt++)
        {
            using var client = _httpClientFactory.CreateClient("UmbracoAutomate");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);

            using var request = new HttpRequestMessage(method, requestUri);
            if (jsonBody is not null)
            {
                request.Content = JsonContent.Create(jsonBody);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            var rawBody = response.Content.Headers.ContentLength is 0
                ? null
                : await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                JsonElement? json = null;
                if (!string.IsNullOrWhiteSpace(rawBody))
                {
                    json = JsonDocument.Parse(rawBody).RootElement.Clone();
                }

                return new SalesforceApiResult { StatusCode = response.StatusCode, IsSuccess = true, Json = json };
            }

            var error = SalesforceErrorMapper.Map(response.StatusCode, rawBody);
            var isRateLimited = response.StatusCode == HttpStatusCode.TooManyRequests
                || error.ErrorCode == SalesforceErrorCode;

            if (!isRateLimited || attempt >= maxAttempts)
            {
                return new SalesforceApiResult { StatusCode = response.StatusCode, IsSuccess = false, Error = error };
            }

            var delay = ComputeRetryDelay(response.Headers.RetryAfter, attempt);
            _logger.LogWarning(
                "Salesforce rate-limited request {Method} {Path} (attempt {Attempt}/{MaxAttempts}) — retrying in {Delay}.",
                method, relativePath, attempt, maxAttempts, delay);
            await Task.Delay(delay, cancellationToken);
        }
    }

    /// <summary>
    /// Honors Salesforce's <c>Retry-After</c> header when present; otherwise falls back to
    /// exponential backoff with jitter (CLAUDE.md §2 non-negotiable #7, §8).
    /// </summary>
    private static TimeSpan ComputeRetryDelay(RetryConditionHeaderValue? retryAfter, int attempt)
    {
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var untilDate = date - DateTimeOffset.UtcNow;
            if (untilDate > TimeSpan.Zero)
            {
                return untilDate;
            }
        }

        var baseDelayMs = Math.Pow(2, attempt) * 500;
        var jitterMs = Random.Shared.Next(0, 250);
        return TimeSpan.FromMilliseconds(baseDelayMs + jitterMs);
    }
}
