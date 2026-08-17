using Umbraco.Automate.Salesforce.Api;

namespace Umbraco.Automate.Salesforce.Tests.Unit;

public class SalesforceSoqlEscaperTests
{
    [Theory]
    [InlineData("Acme", "Acme")]
    [InlineData("O'Brien", "O\\'Brien")]
    [InlineData(@"back\slash", @"back\\slash")]
    [InlineData("' OR '1'='1", "\\' OR \\'1\\'=\\'1")]
    public void EscapeStringLiteral_EscapesBackslashAndQuote(string input, string expected)
    {
        SalesforceSoqlEscaper.EscapeStringLiteral(input).ShouldBe(expected);
    }

    [Fact]
    public void EscapeStringLiteral_EscapedValue_CannotBreakOutOfLiteral()
    {
        var malicious = "x' OR Name != ''";
        var escaped = SalesforceSoqlEscaper.EscapeStringLiteral(malicious);
        var query = $"SELECT Id FROM Account WHERE Name = '{escaped}'";

        // The escaped value must contain exactly one pair of (now-escaped) quotes worth of
        // real quote characters — i.e. none unescaped — so it can't terminate the literal early.
        var unescapedQuoteCount = System.Text.RegularExpressions.Regex.Matches(escaped, @"(?<!\\)'").Count;
        unescapedQuoteCount.ShouldBe(0);
        query.ShouldBe("SELECT Id FROM Account WHERE Name = 'x\\' OR Name != \\'\\''");
    }
}
