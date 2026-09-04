using Microsoft.Extensions.Logging;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Tests.Unit;

public class SalesforceConnectionResolverTests
{
    private static SalesforceConnectionResolver NewResolver(IOAuthCredentialsService service)
        => new(service, Mock.Of<ILogger<SalesforceConnectionResolver>>());

    // Concurrent ForceRefreshAsync calls for the same credentialsId must be coordinated so two
    // automation steps hitting INVALID_SESSION_ID at the same moment can't both redeem the
    // refresh token — risky against a Connected App Refresh Token Policy that
    // rotates/invalidates the previous refresh token on each use.

    private static OAuthCredentials NewCredentials(Guid id, string instanceUrl = "https://na1.salesforce.com")
        => new()
        {
            Id = id,
            Provider = "Salesforce",
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            AccountLabel = instanceUrl,
        };

    [Fact]
    public async Task ForceRefreshAsync_ConcurrentCallsForSameCredentials_OnlyRefreshesOnce()
    {
        var credentialsId = Guid.NewGuid();
        var credentials = NewCredentials(credentialsId);

        var service = new Mock<IOAuthCredentialsService>();
        service.Setup(s => s.GetCredentialsAsync(credentialsId, It.IsAny<CancellationToken>())).ReturnsAsync(credentials);
        service.Setup(s => s.UpdateCredentialsAsync(It.IsAny<OAuthCredentials>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        service.Setup(s => s.GetValidAccessTokenAsync(credentialsId, It.IsAny<CancellationToken>())).ReturnsAsync("refreshed-access-token");

        var resolver = NewResolver(service.Object);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => resolver.ForceRefreshAsync(credentialsId, CancellationToken.None)));

        results.ShouldAllBe(r => r != null && r.AccessToken == "refreshed-access-token");
        service.Verify(s => s.GetValidAccessTokenAsync(credentialsId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForceRefreshAsync_DifferentCredentialsIds_AreNotBlockedByEachOthersLocks()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        var service = new Mock<IOAuthCredentialsService>();
        service.Setup(s => s.GetCredentialsAsync(firstId, It.IsAny<CancellationToken>())).ReturnsAsync(NewCredentials(firstId, "https://na1.salesforce.com"));
        service.Setup(s => s.GetCredentialsAsync(secondId, It.IsAny<CancellationToken>())).ReturnsAsync(NewCredentials(secondId, "https://na2.salesforce.com"));
        service.Setup(s => s.UpdateCredentialsAsync(It.IsAny<OAuthCredentials>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        service.Setup(s => s.GetValidAccessTokenAsync(firstId, It.IsAny<CancellationToken>())).ReturnsAsync("token-1");
        service.Setup(s => s.GetValidAccessTokenAsync(secondId, It.IsAny<CancellationToken>())).ReturnsAsync("token-2");

        var resolver = NewResolver(service.Object);

        var firstResult = await resolver.ForceRefreshAsync(firstId, CancellationToken.None);
        var secondResult = await resolver.ForceRefreshAsync(secondId, CancellationToken.None);

        firstResult!.AccessToken.ShouldBe("token-1");
        secondResult!.AccessToken.ShouldBe("token-2");
        service.Verify(s => s.GetValidAccessTokenAsync(firstId, It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(s => s.GetValidAccessTokenAsync(secondId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForceRefreshAsync_FailedRefresh_IsNotCachedSoTheNextCallRetriesInsteadOfReusingTheFailure()
    {
        // A failed refresh's null result must not be cached for the 5-second freshness window
        // the way a successful one is — otherwise one transient blip on Salesforce's token
        // endpoint would get replayed as a failure to every other caller for the rest of that
        // window instead of each getting its own attempt.
        var credentialsId = Guid.NewGuid();
        var credentials = NewCredentials(credentialsId);

        var service = new Mock<IOAuthCredentialsService>();
        service.Setup(s => s.GetCredentialsAsync(credentialsId, It.IsAny<CancellationToken>())).ReturnsAsync(credentials);
        service.Setup(s => s.UpdateCredentialsAsync(It.IsAny<OAuthCredentials>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        // Empty access token simulates a failed refresh (e.g. the refresh token itself was
        // revoked) — ResolveCoreAsync treats this as "resolution failed", returning null.
        service.Setup(s => s.GetValidAccessTokenAsync(credentialsId, It.IsAny<CancellationToken>())).ReturnsAsync(string.Empty);

        var resolver = NewResolver(service.Object);

        var first = await resolver.ForceRefreshAsync(credentialsId, CancellationToken.None);
        var second = await resolver.ForceRefreshAsync(credentialsId, CancellationToken.None);

        first.ShouldBeNull();
        second.ShouldBeNull();
        // Both calls land inside the same 5-second freshness window in practice (this test runs
        // in milliseconds) — if the failure had been cached, the second call would short-circuit
        // without calling GetValidAccessTokenAsync again.
        service.Verify(s => s.GetValidAccessTokenAsync(credentialsId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ForceRefreshAsync_CredentialsMissing_ReturnsNullWithoutAttemptingRefresh()
    {
        var credentialsId = Guid.NewGuid();

        var service = new Mock<IOAuthCredentialsService>();
        service.Setup(s => s.GetCredentialsAsync(credentialsId, It.IsAny<CancellationToken>())).ReturnsAsync((OAuthCredentials?)null);

        var resolver = NewResolver(service.Object);

        var result = await resolver.ForceRefreshAsync(credentialsId, CancellationToken.None);

        result.ShouldBeNull();
        service.Verify(s => s.UpdateCredentialsAsync(It.IsAny<OAuthCredentials>(), It.IsAny<CancellationToken>()), Times.Never);
        service.Verify(s => s.GetValidAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Every action attaches the live Bearer access token to whatever host InstanceUrl resolves
    // to (SalesforceClient.SendAsync) — resolving to an untrusted host would hand a real
    // Salesforce access token to an arbitrary server. ResolveAsync must refuse to resolve a
    // connection whose stored AccountLabel isn't an HTTPS *.salesforce.com/*.force.com host,
    // even though that value should only ever come from Salesforce's own token response.
    [Theory]
    [InlineData("http://na1.salesforce.com")] // not HTTPS
    [InlineData("https://attacker.example.com")] // wrong host entirely
    [InlineData("https://salesforce.com.attacker.example.com")] // suffix trick, not a real subdomain
    [InlineData("https://evil-salesforce.com")] // similar-looking, not a subdomain
    [InlineData("not a url")] // unparsable
    public async Task ResolveAsync_UntrustedInstanceUrl_ReturnsNull(string instanceUrl)
    {
        var credentialsId = Guid.NewGuid();
        var credentials = NewCredentials(credentialsId, instanceUrl);

        var service = new Mock<IOAuthCredentialsService>();
        service.Setup(s => s.GetCredentialsAsync(credentialsId, It.IsAny<CancellationToken>())).ReturnsAsync(credentials);
        service.Setup(s => s.GetValidAccessTokenAsync(credentialsId, It.IsAny<CancellationToken>())).ReturnsAsync("access-token");

        var resolver = NewResolver(service.Object);

        var result = await resolver.ResolveAsync(credentialsId, CancellationToken.None);

        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("https://na1.salesforce.com")]
    [InlineData("https://mycompany.my.salesforce.com")]
    [InlineData("https://mysite.force.com")]
    public async Task ResolveAsync_TrustedInstanceUrl_Resolves(string instanceUrl)
    {
        var credentialsId = Guid.NewGuid();
        var credentials = NewCredentials(credentialsId, instanceUrl);

        var service = new Mock<IOAuthCredentialsService>();
        service.Setup(s => s.GetCredentialsAsync(credentialsId, It.IsAny<CancellationToken>())).ReturnsAsync(credentials);
        service.Setup(s => s.GetValidAccessTokenAsync(credentialsId, It.IsAny<CancellationToken>())).ReturnsAsync("access-token");

        var resolver = NewResolver(service.Object);

        var result = await resolver.ResolveAsync(credentialsId, CancellationToken.None);

        result.ShouldNotBeNull();
        result!.InstanceUrl.ToString().ShouldBe(new Uri(instanceUrl).ToString());
    }
}
