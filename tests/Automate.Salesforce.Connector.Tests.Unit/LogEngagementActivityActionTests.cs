using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Settings;
using Automate.Salesforce.Connector.Actions;
using Automate.Salesforce.Connector.Api;
using Automate.Salesforce.Connector.Configuration;
using Automate.Salesforce.Connector.Connection;

namespace Automate.Salesforce.Connector.Tests.Unit;

public class LogEngagementActivityActionTests
{
    private static IOptionsMonitor<SalesforceApiOptions> Options()
    {
        var monitor = new Mock<IOptionsMonitor<SalesforceApiOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(new SalesforceApiOptions());
        return monitor.Object;
    }

    private static LogEngagementActivityAction CreateAction(ISalesforceClient? client = null)
        => new(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            Mock.Of<ISalesforceConnectionResolver>(),
            client ?? Mock.Of<ISalesforceClient>(),
            Options());

    private static ActionContext ContextWith(LogEngagementActivitySettings settings, ConfiguredConnection? connection = null) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "salesforce.logEngagementActivity",
        Settings = settings,
        Connection = connection,
    };

    [Fact]
    public async Task ExecuteAsync_MissingWhoId_FailsValidation()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(
            ContextWith(new LogEngagementActivitySettings { Subject = "Downloaded Pricing Guide" }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_MissingSubject_FailsValidation()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(
            ContextWith(new LogEngagementActivitySettings { WhoId = "003xx0000000001" }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_NoConnectionConfigured_FailsValidation()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(
            ContextWith(new LogEngagementActivitySettings { WhoId = "003xx0000000001", Subject = "Downloaded Pricing Guide" }),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }
}
