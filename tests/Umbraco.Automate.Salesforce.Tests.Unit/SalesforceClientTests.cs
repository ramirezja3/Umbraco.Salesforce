using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Tests.Unit;

/// <summary>
/// Regression coverage for the session-refresh-and-retry behavior added after a live-org
/// failure (see docs/dev-notes.md §0a): Salesforce's Web Server OAuth flow returns no <c>expires_in</c>,
/// so <c>Umbraco.Automate.OpenIddict</c>'s locally-tracked token expiry never trips and this
/// package never proactively refreshes. The only signal is Salesforce itself rejecting a call
/// with <c>INVALID_SESSION_ID</c> — <see cref="SalesforceClient"/> must recover from that once
/// via <see cref="ISalesforceConnectionResolver.ForceRefreshAsync"/> rather than requiring the
/// implementer to manually reconnect after every session timeout.
/// </summary>
public class SalesforceClientTests
{
    private static readonly Guid CredentialsId = Guid.NewGuid();
    private static readonly SalesforceConnectionContext StaleConnection =
        new(CredentialsId, "stale-token", new Uri("https://example.my.salesforce.com"));
    private static readonly SalesforceConnectionContext FreshConnection =
        new(CredentialsId, "fresh-token", new Uri("https://example.my.salesforce.com"));

    private const string InvalidSessionBody = """[{"message":"Session expired or invalid","errorCode":"INVALID_SESSION_ID"}]""";

    private static SalesforceClient CreateClient(
        FakeHttpMessageHandler handler, Mock<ISalesforceConnectionResolver> resolver)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));

        var options = new Mock<IOptionsMonitor<SalesforceApiOptions>>();
        options.Setup(o => o.CurrentValue).Returns(new SalesforceApiOptions());

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
        // Regression: the identity/userinfo endpoint's plain-text "Bad_OAuth_Token" body (as
        // opposed to the REST Data API's JSON INVALID_SESSION_ID shape) must trigger the exact
        // same refresh-and-retry recovery, via SalesforceErrorMapper mapping both onto the same
        // ErrorCode (docs/dev-notes.md §0a).
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
