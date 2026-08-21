using Umbraco.Automate.Salesforce.Actions;

namespace Umbraco.Automate.Salesforce.Tests.Unit;

public class SalesforceActionSupportTests
{
    // Regression coverage for the senior-engineer bug-hunt pass (docs/dev-notes.md §0a, finding #6):
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
}
