using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.Automate.Salesforce.Api;
using Umbraco.Community.Automate.Salesforce.Configuration;
using Umbraco.Community.Automate.Salesforce.Connection;

namespace Umbraco.Community.Automate.Salesforce.Tests.Unit;

/// <summary>
/// Covers the session-refresh-and-retry behavior: Salesforce's Web Server OAuth flow returns no
/// <c>expires_in</c>, so <c>Umbraco.Automate.OpenIddict</c>'s locally-tracked token expiry never
/// trips and this package never proactively refreshes. The only signal is Salesforce itself
/// rejecting a call with <c>INVALID_SESSION_ID</c> — <see cref="SalesforceClient"/> must recover
/// from that once via <see cref="ISalesforceConnectionResolver.ForceRefreshAsync"/> rather than
/// requiring the implementer to manually reconnect after every session timeout.
/// </summary>
public class SalesforceClientTests
{
    private static readonly Guid CredentialsId = Guid.NewGuid();
    private static readonly SalesforceConnectionContext StaleConnection =
        new(CredentialsId, "stale-token", new Uri("https://example.my.salesforce.com"));
    private static readonly SalesforceConnectionContext FreshConnection =
        new(CredentialsId, "fresh-token", new Uri("https://example.my.salesforce.com"));

    private const string InvalidSessionBody = """[{"message":"Session expired or invalid","errorCode":"INVALID_SESSION_ID"}]""";

    private const string RateLimitedBody = """[{"message":"Request limit exceeded","errorCode":"REQUEST_LIMIT_EXCEEDED"}]""";

    private static SalesforceClient CreateClient(
        HttpMessageHandler handler, Mock<ISalesforceConnectionResolver> resolver, SalesforceApiOptions? apiOptions = null)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));

        var options = new Mock<IOptionsMonitor<SalesforceApiOptions>>();
        options.Setup(o => o.CurrentValue).Returns(apiOptions ?? new SalesforceApiOptions());

        return new SalesforceClient(httpClientFactory.Object, options.Object, resolver.Object, Mock.Of<ILogger<SalesforceClient>>());
    }

    [Fact]
    public async Task SendAsync_InvalidSessionIdThenSuccess_ForcesRefreshAndRetriesWithNewToken()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(InvalidSessionBody) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"id":"001xx000003DGb2AAG"}""") });

        var resolver = new Mock<ISalesforceConnectionResolver>();
        resolver.Setup(r => r.ForceRefreshAsync(CredentialsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreshConnection);

        var client = CreateClient(handler, resolver);

        var result = await client.SendAsync(StaleConnection, HttpMethod.Get, "/services/data/v62.0/sobjects/Lead/001", null, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("stale-token");
        handler.Requests[1].Headers.Authorization!.Parameter.ShouldBe("fresh-token");
        resolver.Verify(r => r.ForceRefreshAsync(CredentialsId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_BadOAuthTokenPlainTextBody_AlsoForcesRefreshAndRetries()
    {
        // The identity/userinfo endpoint's plain-text "Bad_OAuth_Token" body (as opposed to the
        // REST Data API's JSON INVALID_SESSION_ID shape) must trigger the exact same
        // refresh-and-retry recovery, via SalesforceErrorMapper mapping both onto the same ErrorCode.
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("Bad_OAuth_Token") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"organization_id":"00Dxx0000000001"}""") });

        var resolver = new Mock<ISalesforceConnectionResolver>();
        resolver.Setup(r => r.ForceRefreshAsync(CredentialsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreshConnection);

        var client = CreateClient(handler, resolver);

        var result = await client.SendAsync(StaleConnection, HttpMethod.Get, "/services/oauth2/userinfo", null, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[1].Headers.Authorization!.Parameter.ShouldBe("fresh-token");
    }

    [Fact]
    public async Task SendAsync_SessionStillInvalidAfterRefresh_DoesNotLoopForever()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(InvalidSessionBody) },
            new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(InvalidSessionBody) });

        var resolver = new Mock<ISalesforceConnectionResolver>();
        resolver.Setup(r => r.ForceRefreshAsync(CredentialsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreshConnection);

        var client = CreateClient(handler, resolver);

        var result = await client.SendAsync(StaleConnection, HttpMethod.Get, "/services/data/v62.0/sobjects/Lead/001", null, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.ErrorCode.ShouldBe("INVALID_SESSION_ID");
        handler.Requests.Count.ShouldBe(2);
        resolver.Verify(r => r.ForceRefreshAsync(CredentialsId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ForceRefreshReturnsNull_ReturnsOriginalFailureWithoutRetrying()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(InvalidSessionBody) });

        var resolver = new Mock<ISalesforceConnectionResolver>();
        resolver.Setup(r => r.ForceRefreshAsync(CredentialsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalesforceConnectionContext?)null);

        var client = CreateClient(handler, resolver);

        var result = await client.SendAsync(StaleConnection, HttpMethod.Get, "/services/data/v62.0/sobjects/Lead/001", null, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.ErrorCode.ShouldBe("INVALID_SESSION_ID");
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_RateLimited_RetriesMaxRetryAttemptsTimesAfterTheFirstAttempt()
    {
        // MaxRetryAttempts means retries *after* the first attempt, per its doc comment. With
        // MaxRetryAttempts=2, a request that's rate-limited on every attempt should be tried 3
        // times total (1 + 2 retries).
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent(RateLimitedBody) },
            new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent(RateLimitedBody) },
            new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent(RateLimitedBody) });
        var resolver = new Mock<ISalesforceConnectionResolver>();
        var client = CreateClient(handler, resolver, new SalesforceApiOptions
        {
            MaxRetryAttempts = 2,
            MaxRetryDelay = TimeSpan.FromMilliseconds(1),
        });

        var result = await client.SendAsync(StaleConnection, HttpMethod.Get, "/services/data/v62.0/sobjects/Lead/001", null, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.ErrorCode.ShouldBe("REQUEST_LIMIT_EXCEEDED");
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SendAsync_RateLimitedRepeatedly_DelaysAreCappedToMaxRetryDelay()
    {
        // Uncapped exponential backoff (2^attempt * 500ms) would make this test take ~31 seconds
        // by attempt 5. Capping every delay to a small MaxRetryDelay keeps it fast, proving the
        // cap is actually applied.
        var responses = Enumerable.Range(0, 6)
            .Select(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent(RateLimitedBody) })
            .ToArray();
        var handler = new FakeHttpMessageHandler(responses);
        var resolver = new Mock<ISalesforceConnectionResolver>();
        var client = CreateClient(handler, resolver, new SalesforceApiOptions
        {
            MaxRetryAttempts = 5,
            MaxRetryDelay = TimeSpan.FromMilliseconds(10),
        });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await client.SendAsync(StaleConnection, HttpMethod.Get, "/services/data/v62.0/sobjects/Lead/001", null, CancellationToken.None);
        stopwatch.Stop();

        result.IsSuccess.ShouldBeFalse();
        handler.Requests.Count.ShouldBe(6);
        // 5 retries * 10ms cap = 50ms of intended delay; generous ceiling to absorb test-runner
        // overhead while still failing fast if the cap wasn't actually applied.
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task SendAsync_NetworkFailure_ReturnsMappedErrorInsteadOfThrowing()
    {
        // A DNS failure/connection refused/TLS error must map to a clear SalesforceApiError
        // instead of propagating as a raw HttpRequestException straight to the Run log.
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused"));
        var resolver = new Mock<ISalesforceConnectionResolver>();
        var client = CreateClient(handler, resolver);

        var result = await client.SendAsync(StaleConnection, HttpMethod.Get, "/services/data/v62.0/sobjects/Lead/001", null, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        result.Error!.Message.ShouldContain("Could not reach Salesforce");
        resolver.Verify(r => r.ForceRefreshAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_CallerCancellation_PropagatesInsteadOfBeingMappedAsNetworkFailure()
    {
        var handler = new ThrowingHttpMessageHandler(new TaskCanceledException());
        var resolver = new Mock<ISalesforceConnectionResolver>();
        var client = CreateClient(handler, resolver);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<TaskCanceledException>(() =>
            client.SendAsync(StaleConnection, HttpMethod.Get, "/services/data/v62.0/sobjects/Lead/001", null, cts.Token));
    }
}

/// <summary>
/// Records every request it receives and replays canned responses in order.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses;

    public FakeHttpMessageHandler(params HttpResponseMessage[] responses)
    {
        _responses = new Queue<HttpResponseMessage>(responses);
    }

    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_responses.Dequeue());
    }
}

/// <summary>
/// Throws the given exception instead of returning a response — simulates a DNS failure,
/// connection refused, TLS handshake error, or timeout at the transport level.
/// </summary>
internal sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    private readonly Exception _exception;

    public ThrowingHttpMessageHandler(Exception exception) => _exception = exception;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => throw _exception;
}
