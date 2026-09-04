using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Settings;
using Umbraco.Community.Automate.Salesforce.Actions;
using Umbraco.Community.Automate.Salesforce.Api;
using Umbraco.Community.Automate.Salesforce.Configuration;
using Umbraco.Community.Automate.Salesforce.Connection;

namespace Umbraco.Community.Automate.Salesforce.Tests.Unit;

public class CreateOpportunityActionTests
{
    private static IOptionsMonitor<SalesforceApiOptions> Options()
    {
        var monitor = new Mock<IOptionsMonitor<SalesforceApiOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(new SalesforceApiOptions());
        return monitor.Object;
    }

    private static CreateOpportunityAction CreateAction(ISalesforceClient? client = null)
        => new(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            Mock.Of<ISalesforceConnectionResolver>(),
            client ?? Mock.Of<ISalesforceClient>(),
            Options());

    private static ActionContext ContextWith(CreateOpportunitySettings settings, ConfiguredConnection? connection = null) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "salesforce.createOpportunity",
        Settings = settings,
        Connection = connection,
    };

    private static CreateOpportunitySettings ValidSettings() => new()
    {
        Name = "Acme Deal",
        StageName = "Prospecting",
        CloseDate = "2026-12-31",
    };

    [Fact]
    public async Task ExecuteAsync_MissingName_FailsValidation()
    {
        var action = CreateAction();
        var settings = ValidSettings();
        settings.Name = string.Empty;

        var result = await action.ExecuteAsync(ContextWith(settings), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_MissingStage_FailsValidation()
    {
        var action = CreateAction();
        var settings = ValidSettings();
        settings.StageName = string.Empty;

        var result = await action.ExecuteAsync(ContextWith(settings), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCloseDate_FailsValidation()
    {
        var action = CreateAction();
        var settings = ValidSettings();
        settings.CloseDate = string.Empty;

        var result = await action.ExecuteAsync(ContextWith(settings), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_NoConnectionConfigured_FailsValidation()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(ContextWith(ValidSettings()), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }
}
