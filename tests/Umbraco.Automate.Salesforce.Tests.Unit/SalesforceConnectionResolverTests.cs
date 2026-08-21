using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Tests.Unit;

public class SalesforceConnectionResolverTests
{
    // Regression coverage for the senior-engineer bug-hunt pass (docs/dev-notes.md §0a, finding #3):
    // concurrent ForceRefreshAsync calls for the same credentialsId previously had no
    // coordination at all, so two automation steps hitting INVALID_SESSION_ID at the same moment
    // could both redeem the refresh token — risky against a Connected App Refresh Token Policy
    // that rotates/invalidates the previous refresh token on each use.

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

        var resolver = new SalesforceConnectionResolver(service.Object);

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

        var resolver = new SalesforceConnectionResolver(service.Object);

        var firstResult = await resolver.ForceRefreshAsync(firstId, CancellationToken.None);
        var secondResult = await resolver.ForceRefreshAsync(secondId, CancellationToken.None);

        firstResult!.AccessToken.ShouldBe("token-1");
        secondResult!.AccessToken.ShouldBe("token-2");
        service.Verify(s => s.GetValidAccessTokenAsync(firstId, It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(s => s.GetValidAccessTokenAsync(secondId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForceRefreshAsync_CredentialsMissing_ReturnsNullWithoutAttemptingRefresh()
    {
        var credentialsId = Guid.NewGuid();

        var service = new Mock<IOAuthCredentialsService>();
        service.Setup(s => s.GetCredentialsAsync(credentialsId, It.IsAny<CancellationToken>())).ReturnsAsync((OAuthCredentials?)null);

        var resolver = new SalesforceConnectionResolver(service.Object);

        var result = await resolver.ForceRefreshAsync(credentialsId, CancellationToken.None);

        result.ShouldBeNull();
        service.Verify(s => s.UpdateCredentialsAsync(It.IsAny<OAuthCredentials>(), It.IsAny<CancellationToken>()), Times.Never);
        service.Verify(s => s.GetValidAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
