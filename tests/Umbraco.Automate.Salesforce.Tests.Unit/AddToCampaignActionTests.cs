using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Salesforce.Actions;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Tests.Unit;

public class AddToCampaignActionTests
{
    private static IOptionsMonitor<SalesforceApiOptions> Options()
    {
        var monitor = new Mock<IOptionsMonitor<SalesforceApiOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(new SalesforceApiOptions());
        return monitor.Object;
    }

    private static AddToCampaignAction CreateAction(ISalesforceClient? client = null)
        => new(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            Mock.Of<ISalesforceConnectionResolver>(),
            client ?? Mock.Of<ISalesforceClient>(),
            Options());

    private static ActionContext ContextWith(AddToCampaignSettings settings, ConfiguredConnection? connection = null) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "salesforce.addToCampaign",
        Settings = settings,
        Connection = connection,
    };

    [Fact]
    public async Task ExecuteAsync_MissingCampaignId_FailsValidation()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(
            ContextWith(new AddToCampaignSettings { ContactId = "003xx0000000001" }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_NeitherContactNorLeadId_FailsValidation()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(
            ContextWith(new AddToCampaignSettings { CampaignId = "701xx0000000001" }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_BothContactAndLeadId_FailsValidation()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(
            ContextWith(new AddToCampaignSettings
            {
                CampaignId = "701xx0000000001",
                ContactId = "003xx0000000001",
                LeadId = "00Qxx0000000001",
            }),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_NoConnectionConfigured_FailsValidation()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(
            ContextWith(new AddToCampaignSettings { CampaignId = "701xx0000000001", ContactId = "003xx0000000001" }),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }
}
