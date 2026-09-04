using System.Net;
using Umbraco.Automate.Core.Actions;
using Automate.Salesforce.Connector.Actions;
using Automate.Salesforce.Connector.Api;

namespace Automate.Salesforce.Connector.Tests.Unit;

public class SalesforceActionSupportTests
{
    // CreateLeadAction (and any other action with named fields + an AdditionalFields escape
    // hatch) relies on TryParseFields' dictionary being case-insensitive so that assigning a
    // named field on top of it (e.g. fields["Email"] = settings.Email;) correctly overrides a
    // differently-cased duplicate already parsed from AdditionalFields, rather than both ending
    // up in the outgoing JSON body.

    [Fact]
    public void TryParseFields_NamedFieldAssignedAfterParsing_OverridesDifferentlyCasedDuplicate()
    {
        var failure = SalesforceActionSupport.TryParseFields("""{"email":"from-additional-fields@example.com"}""", out var fields);

        failure.ShouldBeNull();

        // Mirrors CreateLeadAction's own "named fields always win" assignment.
        fields["Email"] = "from-named-field@example.com";

        fields.Count.ShouldBe(1);
        fields["email"].ShouldBe("from-named-field@example.com");
        fields["Email"].ShouldBe("from-named-field@example.com");
    }

    [Fact]
    public void TryParseFields_JsonItselfHasCaseVariantDuplicateKeys_LastOneWinsWithoutThrowing()
    {
        var failure = SalesforceActionSupport.TryParseFields("""{"Email":"first@example.com","email":"second@example.com"}""", out var fields);

        failure.ShouldBeNull();
        fields.Count.ShouldBe(1);
        // Unmerged JSON scalar values deserialize as boxed JsonElement, not a plain string.
        ((System.Text.Json.JsonElement)fields["EMAIL"]!).GetString().ShouldBe("second@example.com");
    }

    [Fact]
    public void TryParseFields_NullOrWhitespaceJson_ReturnsEmptyCaseInsensitiveDictionary()
    {
        var failure = SalesforceActionSupport.TryParseFields(null, out var fields);

        failure.ShouldBeNull();
        fields.ShouldBeEmpty();

        fields["AnyKey"] = "value";
        fields["anykey"].ShouldBe("value");
    }

    [Fact]
    public void TryParseFields_InvalidJson_ReturnsValidationFailure()
    {
        var failure = SalesforceActionSupport.TryParseFields("{not json", out var fields);

        failure.ShouldNotBeNull();
        fields.ShouldBeEmpty();
    }

    [Fact]
    public void TryParseFields_InvalidJson_MessageNamesExpectedShapeInsteadOfRawParserError()
    {
        // The error must name the expected shape rather than surface the raw System.Text.Json
        // parser message verbatim (e.g. "'T' is an invalid start of a value. Path: $ |
        // LineNumber: 0 | BytePositionInLine: 0.").
        var failure = SalesforceActionSupport.TryParseFields("This is plain text, not JSON", out _);

        failure.ShouldNotBeNull();
        failure.Exception!.Message.ShouldContain("JSON object of field API names to values");
        failure.Exception.Message.ShouldContain("This is plain text, not JSON");
        failure.Exception.Message.ShouldNotContain("BytePositionInLine");
    }

    [Fact]
    public void Failed_MapsMessageAndCategoryFromTheAlreadyMappedError()
    {
        var result = new SalesforceApiResult
        {
            StatusCode = HttpStatusCode.BadRequest,
            IsSuccess = false,
            Error = new SalesforceApiError("A required Salesforce field is missing: LastName", "REQUIRED_FIELD_MISSING", StepRunErrorCategory.Validation),
        };

        var failure = SalesforceActionSupport.Failed(result);

        failure.Exception!.Message.ShouldBe("A required Salesforce field is missing: LastName");
        failure.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }
}
