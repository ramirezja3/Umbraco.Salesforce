using System.Text.Json;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Connection;
using Umbraco.Automate.Salesforce.Persistence;
using Umbraco.Automate.Salesforce.Triggers;

namespace Umbraco.Automate.Salesforce.Tests.Unit;

public class PollingTriggerTests
{
    private static readonly SalesforceConnectionContext DummyConnection =
        new("token", new Uri("https://example.my.salesforce.com"));

    private static TriggerInfrastructure Infrastructure()
        => new(Mock.Of<IEditableModelResolver>());

    private static IOptionsMonitor<SalesforceApiOptions> Options(SalesforceApiOptions? options = null)
    {
        var monitor = new Mock<IOptionsMonitor<SalesforceApiOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options ?? new SalesforceApiOptions());
        return monitor.Object;
    }

    private static SalesforceApiResult SuccessResult(string json)
        => new()
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            IsSuccess = true,
            Json = JsonDocument.Parse(json).RootElement,
        };

    private static SalesforcePollingContext ContextFor(
        object? settings, SalesforcePollingState previousState, ISalesforceClient client, DateTime pollStartedUtc)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            TriggerAlias = "salesforce.test",
            Settings = settings,
            Connection = DummyConnection,
            Client = client,
            PreviousState = previousState,
            PollStartedUtc = pollStartedUtc,
        };

    [Fact]
    public async Task RecordCreatedTrigger_FirstPollEver_EstablishesBaselineWithoutFiring()
    {
        var trigger = new RecordCreatedTrigger(Infrastructure(), Options());
        var pollStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var context = ContextFor(
            new RecordCreatedTriggerSettings { ObjectApiName = "Lead" },
            SalesforcePollingState.Initial,
            Mock.Of<ISalesforceClient>(),
            pollStart);

        var result = await trigger.PollAsync(context, CancellationToken.None);

        result.Events.ShouldBeEmpty();
        result.NextState.LastPollUtc.ShouldBe(pollStart);
    }

    [Fact]
    public async Task RecordCreatedTrigger_SubsequentPoll_FiresOneEventPerNewRecord()
    {
        var client = new Mock<ISalesforceClient>();
        client.Setup(c => c.SendAsync(DummyConnection, HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("""
                {"totalSize":1,"done":true,"records":[
                  {"attributes":{"type":"Lead"},"Id":"00Qxx0000000001","CreatedDate":"2026-01-02T00:00:00.000+0000"}
                ]}
                """));

        var trigger = new RecordCreatedTrigger(Infrastructure(), Options());
        var context = ContextFor(
            new RecordCreatedTriggerSettings { ObjectApiName = "Lead" },
            new SalesforcePollingState(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new Dictionary<string, string>()),
            client.Object,
            new DateTime(2026, 1, 2, 0, 5, 0, DateTimeKind.Utc));

        var result = await trigger.PollAsync(context, CancellationToken.None);

        result.Events.Count.ShouldBe(1);
        var output = ((TriggerEvent<RecordCreatedTriggerOutput>)result.Events[0]).Output;
        output.RecordId.ShouldBe("00Qxx0000000001");
        result.Events[0].TargetAutomationId.ShouldBe(context.AutomationId);
        result.Events[0].IdempotencyKey.ShouldNotBeNull().ShouldContain("00Qxx0000000001");
    }

    [Fact]
    public async Task OpportunityStageChangedTrigger_FirstObservationOfARecord_SeedsButDoesNotFire()
    {
        var client = new Mock<ISalesforceClient>();
        client.Setup(c => c.SendAsync(DummyConnection, HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("""
                {"totalSize":1,"done":true,"records":[
                  {"attributes":{"type":"Opportunity"},"Id":"006xx0000000001","StageName":"Prospecting","Amount":1000.0,"AccountId":"001xx","OwnerId":"005xx","LastModifiedDate":"2026-01-02T00:00:00.000+0000"}
                ]}
                """));

        var trigger = new OpportunityStageChangedTrigger(Infrastructure(), Options());
        var context = ContextFor(
            new OpportunityStageChangedTriggerSettings(),
            SalesforcePollingState.Initial,
            client.Object,
            new DateTime(2026, 1, 2, 0, 5, 0, DateTimeKind.Utc));

        var result = await trigger.PollAsync(context, CancellationToken.None);

        result.Events.ShouldBeEmpty();
        result.NextState.Snapshot["006xx0000000001"].ShouldBe("Prospecting");
    }

    [Fact]
    public async Task OpportunityStageChangedTrigger_StageChangesOnSubsequentPoll_Fires()
    {
        var client = new Mock<ISalesforceClient>();
        client.Setup(c => c.SendAsync(DummyConnection, HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("""
                {"totalSize":1,"done":true,"records":[
                  {"attributes":{"type":"Opportunity"},"Id":"006xx0000000001","StageName":"Closed Won","Amount":5000.0,"AccountId":"001xx","OwnerId":"005xx","LastModifiedDate":"2026-01-03T00:00:00.000+0000"}
                ]}
                """));

        var trigger = new OpportunityStageChangedTrigger(Infrastructure(), Options());
        var previousState = new SalesforcePollingState(
            new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            new Dictionary<string, string> { ["006xx0000000001"] = "Prospecting" });
        var context = ContextFor(
            new OpportunityStageChangedTriggerSettings(),
            previousState,
            client.Object,
            new DateTime(2026, 1, 3, 0, 5, 0, DateTimeKind.Utc));

        var result = await trigger.PollAsync(context, CancellationToken.None);

        result.Events.Count.ShouldBe(1);
        var output = ((TriggerEvent<OpportunityStageChangedTriggerOutput>)result.Events[0]).Output;
        output.PreviousStage.ShouldBe("Prospecting");
        output.NewStage.ShouldBe("Closed Won");
        result.NextState.Snapshot["006xx0000000001"].ShouldBe("Closed Won");
    }

    [Fact]
    public async Task OpportunityStageChangedTrigger_TargetStageFilter_SkipsNonMatchingChanges()
    {
        var client = new Mock<ISalesforceClient>();
        client.Setup(c => c.SendAsync(DummyConnection, HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("""
                {"totalSize":1,"done":true,"records":[
                  {"attributes":{"type":"Opportunity"},"Id":"006xx0000000001","StageName":"Negotiation","Amount":5000.0,"AccountId":"001xx","OwnerId":"005xx","LastModifiedDate":"2026-01-03T00:00:00.000+0000"}
                ]}
                """));

        var trigger = new OpportunityStageChangedTrigger(Infrastructure(), Options());
        var previousState = new SalesforcePollingState(
            new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            new Dictionary<string, string> { ["006xx0000000001"] = "Prospecting" });
        var context = ContextFor(
            new OpportunityStageChangedTriggerSettings { TargetStage = "Closed Won" },
            previousState,
            client.Object,
            new DateTime(2026, 1, 3, 0, 5, 0, DateTimeKind.Utc));

        var result = await trigger.PollAsync(context, CancellationToken.None);

        result.Events.ShouldBeEmpty();
        // Snapshot still advances even when the target-stage filter suppresses the event.
        result.NextState.Snapshot["006xx0000000001"].ShouldBe("Negotiation");
    }

    [Fact]
    public async Task LeadConvertedTrigger_SameLeadAcrossTwoPolls_OnlyFiresOnce()
    {
        var client = new Mock<ISalesforceClient>();
        client.Setup(c => c.SendAsync(DummyConnection, HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("""
                {"totalSize":1,"done":true,"records":[
                  {"attributes":{"type":"Lead"},"Id":"00Qxx0000000001","ConvertedContactId":"003xx","ConvertedAccountId":"001xx","ConvertedOpportunityId":"006xx","LastModifiedDate":"2026-01-03T00:00:00.000+0000"}
                ]}
                """));

        var trigger = new LeadConvertedTrigger(Infrastructure(), Options());
        var firstPollState = new SalesforcePollingState(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), new Dictionary<string, string>());
        var firstContext = ContextFor(null, firstPollState, client.Object, new DateTime(2026, 1, 3, 0, 5, 0, DateTimeKind.Utc));

        var firstResult = await trigger.PollAsync(firstContext, CancellationToken.None);
        firstResult.Events.Count.ShouldBe(1);

        // Second poll: the same Lead is touched again (e.g. a description edit) and re-enters
        // the LastModifiedDate window — must not fire a second time.
        var secondContext = ContextFor(
            null,
            new SalesforcePollingState(new DateTime(2026, 1, 3, 0, 5, 0, DateTimeKind.Utc), firstResult.NextState.Snapshot),
            client.Object,
            new DateTime(2026, 1, 3, 0, 10, 0, DateTimeKind.Utc));

        var secondResult = await trigger.PollAsync(secondContext, CancellationToken.None);
        secondResult.Events.ShouldBeEmpty();
    }
}
