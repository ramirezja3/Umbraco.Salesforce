using System.Net;
using Umbraco.Automate.Core.Actions;
using Automate.Salesforce.Connector.Api;

namespace Automate.Salesforce.Connector.Tests.Unit;

public class SalesforceErrorMapperTests
{
    [Fact]
    public void Map_RequestLimitExceeded_CategorizesAsRateLimiting()
    {
        var body = """[{"message":"TotalRequests Limit exceeded.","errorCode":"REQUEST_LIMIT_EXCEEDED"}]""";

        var error = SalesforceErrorMapper.Map(HttpStatusCode.Forbidden, body);

        error.Category.ShouldBe(StepRunErrorCategory.RateLimiting);
        error.ErrorCode.ShouldBe("REQUEST_LIMIT_EXCEEDED");
        error.Message.ShouldContain("request limit exceeded", Case.Insensitive);
    }

    [Fact]
    public void Map_InvalidSessionId_CategorizesAsAuthentication()
    {
        var body = """[{"message":"Session expired or invalid","errorCode":"INVALID_SESSION_ID"}]""";

        var error = SalesforceErrorMapper.Map(HttpStatusCode.Unauthorized, body);

        error.Category.ShouldBe(StepRunErrorCategory.Authentication);
        error.Message.ShouldContain("session is no longer valid", Case.Insensitive);
    }

    [Fact]
    public void Map_RequiredFieldMissing_CategorizesAsValidationAndIncludesMessage()
    {
        var body = """[{"message":"Required fields are missing: [LastName]","errorCode":"REQUIRED_FIELD_MISSING","fields":["LastName"]}]""";

        var error = SalesforceErrorMapper.Map(HttpStatusCode.BadRequest, body);

        error.Category.ShouldBe(StepRunErrorCategory.Validation);
        error.Message.ShouldContain("LastName");
    }

    [Fact]
    public void Map_DuplicatesDetected_CategorizesAsValidationWithClearMessage()
    {
        // Salesforce's Duplicate Rules feature (distinct from DUPLICATE_VALUE's unique-field
        // violations) — confirmed live: creating a Contact with the same email as an existing
        // Lead triggers this with a bare, UI-oriented message ("Use one of these records?") that
        // means nothing without context.
        var body = """[{"message":"Use one of these records?","errorCode":"DUPLICATES_DETECTED"}]""";

        var error = SalesforceErrorMapper.Map(HttpStatusCode.BadRequest, body);

        error.Category.ShouldBe(StepRunErrorCategory.Validation);
        error.Message.ShouldContain("Duplicate Rules", Case.Insensitive);
        error.Message.ShouldNotContain("Use one of these records?");
    }

    [Fact]
    public void Map_OAuthErrorShape_ParsesErrorAndDescription()
    {
        var body = """{"error":"invalid_grant","error_description":"expired access/refresh token"}""";

        var error = SalesforceErrorMapper.Map(HttpStatusCode.BadRequest, body);

        error.Category.ShouldBe(StepRunErrorCategory.Authentication);
        error.ErrorCode.ShouldBe("INVALID_GRANT");
    }

    [Fact]
    public void Map_ServerError_CategorizesAsServiceUnavailable()
    {
        var error = SalesforceErrorMapper.Map(HttpStatusCode.InternalServerError, rawBody: null);

        error.Category.ShouldBe(StepRunErrorCategory.ServiceUnavailable);
        error.ErrorCode.ShouldBeNull();
    }

    [Fact]
    public void Map_MalformedBody_FallsBackToStatusCodeWithoutThrowing()
    {
        var error = SalesforceErrorMapper.Map(HttpStatusCode.BadGateway, "<html>not json</html>");

        error.Message.ShouldContain("502");
    }

    [Fact]
    public void Map_TooManyRequests_CategorizesAsRateLimitingEvenWithoutErrorCode()
    {
        var error = SalesforceErrorMapper.Map(HttpStatusCode.TooManyRequests, rawBody: null);

        error.Category.ShouldBe(StepRunErrorCategory.RateLimiting);
    }

    [Fact]
    public void Map_BadOAuthTokenPlainTextBody_MapsOntoInvalidSessionId()
    {
        // Salesforce's identity endpoints (/services/oauth2/userinfo, /id/...) return a bare
        // plain-text token on failure, not JSON. Must map onto the same INVALID_SESSION_ID code
        // the REST Data API's JSON shape uses, so SalesforceClient's force-refresh-and-retry
        // triggers for this shape too, not just the JSON one.
        var error = SalesforceErrorMapper.Map(HttpStatusCode.Forbidden, "Bad_OAuth_Token");

        error.ErrorCode.ShouldBe("INVALID_SESSION_ID");
        error.Category.ShouldBe(StepRunErrorCategory.Authentication);
        error.Message.ShouldContain("session is no longer valid", Case.Insensitive);
    }
}
