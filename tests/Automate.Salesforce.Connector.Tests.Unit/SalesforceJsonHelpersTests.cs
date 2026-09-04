using Automate.Salesforce.Connector.Api;

namespace Automate.Salesforce.Connector.Tests.Unit;

public class SalesforceJsonHelpersTests
{
    // `fields["Amount"] as double?` silently evaluates to null for any whole-dollar Opportunity,
    // because a boxed long can never satisfy `as double?`. ToDouble must handle every boxed
    // numeric CLR type ToFieldDictionary's Unwrap can actually produce (long for whole-number
    // JSON literals, double for anything with a decimal point) plus the other boxed value types
    // it could plausibly be handed.

    [Fact]
    public void ToDouble_BoxedLong_ConvertsRatherThanReturningNull()
    {
        object? boxed = 5000L;

        SalesforceJsonHelpers.ToDouble(boxed).ShouldBe(5000d);
    }

    [Fact]
    public void ToDouble_BoxedDouble_ReturnsSameValue()
    {
        object? boxed = 5000.5d;

        SalesforceJsonHelpers.ToDouble(boxed).ShouldBe(5000.5d);
    }

    [Fact]
    public void ToDouble_BoxedInt_Converts()
    {
        object? boxed = 42;

        SalesforceJsonHelpers.ToDouble(boxed).ShouldBe(42d);
    }

    [Fact]
    public void ToDouble_BoxedDecimal_Converts()
    {
        object? boxed = 12.34m;

        SalesforceJsonHelpers.ToDouble(boxed).ShouldBe(12.34d);
    }

    [Fact]
    public void ToDouble_BoxedFloat_Converts()
    {
        object? boxed = 1.5f;

        SalesforceJsonHelpers.ToDouble(boxed).ShouldBe(1.5d);
    }

    [Fact]
    public void ToDouble_Null_ReturnsNull()
    {
        SalesforceJsonHelpers.ToDouble(null).ShouldBeNull();
    }

    [Fact]
    public void ToDouble_UnsupportedType_ReturnsNullRatherThanThrowing()
    {
        object boxed = "not a number";

        SalesforceJsonHelpers.ToDouble(boxed).ShouldBeNull();
    }
}
