using Umbraco.Automate.Salesforce.Actions;

namespace Umbraco.Automate.Salesforce.Tests.Unit;

public class QueryRecordsActionRowLimitTests
{
    [Fact]
    public void ApplyRowLimit_NoExistingLimit_AppendsLimitClause()
    {
        var result = QueryRecordsAction.ApplyRowLimit("SELECT Id FROM Account", 200);

        result.ShouldBe("SELECT Id FROM Account LIMIT 200");
    }

    [Fact]
    public void ApplyRowLimit_ExistingLimitBelowCap_LeavesQueryUnchanged()
    {
        var result = QueryRecordsAction.ApplyRowLimit("SELECT Id FROM Account LIMIT 10", 200);

        result.ShouldBe("SELECT Id FROM Account LIMIT 10");
    }

    [Fact]
    public void ApplyRowLimit_ExistingLimitAboveCap_ClampsDownToCap()
    {
        var result = QueryRecordsAction.ApplyRowLimit("SELECT Id FROM Account LIMIT 5000", 200);

        result.ShouldBe("SELECT Id FROM Account LIMIT 200");
    }

    [Fact]
    public void ApplyRowLimit_ExistingLimitAtCap_LeavesQueryUnchanged()
    {
        var result = QueryRecordsAction.ApplyRowLimit("SELECT Id FROM Account LIMIT 200", 200);

        result.ShouldBe("SELECT Id FROM Account LIMIT 200");
    }

    [Fact]
    public void ApplyRowLimit_LimitIsCaseInsensitive()
    {
        var result = QueryRecordsAction.ApplyRowLimit("SELECT Id FROM Account limit 9999", 200);

        result.ShouldBe("SELECT Id FROM Account LIMIT 200");
    }
}
