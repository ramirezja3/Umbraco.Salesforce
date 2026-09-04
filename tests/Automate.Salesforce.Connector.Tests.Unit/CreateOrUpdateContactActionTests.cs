using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Settings;
using Automate.Salesforce.Connector.Actions;
using Automate.Salesforce.Connector.Api;
using Automate.Salesforce.Connector.Configuration;
using Automate.Salesforce.Connector.Connection;

namespace Automate.Salesforce.Connector.Tests.Unit;

public class CreateOrUpdateContactActionTests
{
    private static IOptionsMonitor<SalesforceApiOptions> Options()
    {
        var monitor = new Mock<IOptionsMonitor<SalesforceApiOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(new SalesforceApiOptions());
        return monitor.Object;
    }

    private static CreateOrUpdateContactAction CreateAction(ISalesforceClient? client = null)
        => new(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            Mock.Of<ISalesforceConnectionResolver>(),
            client ?? Mock.Of<ISalesforceClient>(),
            Options());

    private static ActionContext ContextWith(CreateOrUpdateContactSettings settings, ConfiguredConnection? connection = null) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "salesforce.createOrUpdateContact",
        Settings = settings,
        Connection = connection,
    };

    [Fact]
    public async Task ExecuteAsync_MissingLastName_FailsValidation()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(
            ContextWith(new CreateOrUpdateContactSettings()), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidAdditionalFieldsJson_FailsValidation()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(
            ContextWith(new CreateOrUpdateContactSettings { LastName = "Smith", AdditionalFields = "{not json" }),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_NoConnectionConfigured_FailsValidation()
    {
        var action = CreateAction();

        var result = await action.ExecuteAsync(
            ContextWith(new CreateOrUpdateContactSettings { LastName = "Smith" }),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }
}
