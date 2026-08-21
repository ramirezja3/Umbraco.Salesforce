using Umbraco.Automate.Salesforce.Api;

namespace Umbraco.Automate.Salesforce.Tests.Unit;

public class SalesforceFieldListHelperTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("Name", "Name")]
    [InlineData("Name,Email", "Name,Email")]
    [InlineData("Name, Email", "Name,Email")]
    [InlineData(" Name , Email ", "Name,Email")]
    [InlineData("Name,,Email", "Name,Email")]
    [InlineData("Name,Email,", "Name,Email")]
    [InlineData(",Name,Email", "Name,Email")]
    public void CleanCommaSeparatedList_ProducesExpectedResult(string? raw, string expected)
    {
        var result = SalesforceFieldListHelper.CleanCommaSeparatedList(raw);

        result.ShouldBe(expected);
    }
}
