using System.Text.Json;
using Microsoft.Extensions.Logging;
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
        new(Guid.NewGuid(), "token", new Uri("https://example.my.salesforce.com"));

    private static TriggerInfrastructure Infrastructure()
        => new(Mock.Of<IEditableModelResolver>());

    private static IOptionsMonitor<SalesforceApiOptions> Options(SalesforceApiOptions? options = null)
    {
        var monitor = new Mock<IOptionsMonitor<SalesforceApiOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options ?? new SalesforceApiOptions());
        return monitor.Object;
    }

    private static ILogger<T> Logger<T>() => Mock.Of<ILogger<T>>();

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
    public async Task OpportunityStageChangedTrigger_FirstObservationOfARecord_SeedsButDoesNotFire()
    {
        var client = new Mock<ISalesforceClient>();
        client.Setup(c => c.SendAsync(DummyConnection, HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("""
                {"totalSize":1,"done":true,"records":[
                  {"attributes":{"type":"Opportunity"},"Id":"006xx0000000001","StageName":"Prospecting","Amount":1000.0,"AccountId":"001xx","OwnerId":"005xx","LastModifiedDate":"2026-01-02T00:00:00.000+0000"}
                ]}
                """));

        var trigger = new OpportunityStageChangedTrigger(Infrastructure(), Options(), Logger<OpportunityStageChangedTrigger>());
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

        var trigger = new OpportunityStageChangedTrigger(Infrastructure(), Options(), Logger<OpportunityStageChangedTrigger>());
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

        var trigger = new OpportunityStageChangedTrigger(Infrastructure(), Options(), Logger<OpportunityStageChangedTrigger>());
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

    // --- Regression coverage for the actions/triggers edge-case audit (docs/dev-notes.md §0a) ---

    [Fact]
    public async Task OpportunityStageChangedTrigger_ReturnedRecordCountHitsMaxQueryRows_CapsWatermarkToLastRecordTimestamp()
    {
        // Regression: previously the watermark always advanced to PollStartedUtc, so any records
        // beyond MaxQueryRows within this poll's window would be permanently skipped by the next
        // poll's lower bound. MaxQueryRows=1 with exactly 1 record returned simulates "the LIMIT
        // was hit — more records may exist beyond it that this poll never saw."
        // Uses a non-null PreviousState.LastPollUtc (a subsequent poll) rather than
        // SalesforcePollingState.Initial — the first poll now takes the unfiltered seed-sweep
        // path (see the seed-specific tests below), which doesn't exercise MaxQueryRows/watermark
        // capping at all, so this test would no longer cover what it claims to if left on Initial.
        var client = new Mock<ISalesforceClient>();
        client.Setup(c => c.SendAsync(DummyConnection, HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("""
                {"totalSize":1,"done":true,"records":[
                  {"attributes":{"type":"Opportunity"},"Id":"006xx0000000001","StageName":"Closed Won","Amount":1000.0,"AccountId":"001xx","OwnerId":"005xx","LastModifiedDate":"2026-01-02T00:00:00.000+0000"}
                ]}
                """));

        var trigger = new OpportunityStageChangedTrigger(
            Infrastructure(), Options(new SalesforceApiOptions { MaxQueryRows = 1 }), Logger<OpportunityStageChangedTrigger>());
        var previousState = new SalesforcePollingState(
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            new Dictionary<string, string> { ["006xx0000000001"] = "Prospecting" });
        var context = ContextFor(
            new OpportunityStageChangedTriggerSettings(),
            previousState,
            client.Object,
            new DateTime(2026, 1, 2, 0, 5, 0, DateTimeKind.Utc));

        var result = await trigger.PollAsync(context, CancellationToken.None);

        result.NextState.LastPollUtc.ShouldBe(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        result.NextState.LastPollUtc.ShouldNotBe(context.PollStartedUtc);
    }

    [Fact]
    public async Task OpportunityStageChangedTrigger_AmountHasNoDecimalPoint_IsNotSilentlyNull()
    {
        // Regression for finding #1 (docs/dev-notes.md §0a): Salesforce returns a whole-dollar Amount as
        // a JSON number with no decimal point (e.g. 5000, not 5000.0), which ToFieldDictionary
        // boxes as a long — `fields["Amount"] as double?` silently evaluated to null for exactly
        // this shape, since the `as` operator never converts between boxed value types.
        var client = new Mock<ISalesforceClient>();
        client.Setup(c => c.SendAsync(DummyConnection, HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("""
                {"totalSize":1,"done":true,"records":[
                  {"attributes":{"type":"Opportunity"},"Id":"006xx0000000001","StageName":"Closed Won","Amount":5000,"AccountId":"001xx","OwnerId":"005xx","LastModifiedDate":"2026-01-03T00:00:00.000+0000"}
                ]}
                """));

        var trigger = new OpportunityStageChangedTrigger(Infrastructure(), Options(), Logger<OpportunityStageChangedTrigger>());
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
        output.Amount.ShouldBe(5000d);
    }

    [Fact]
    public async Task OpportunityStageChangedTrigger_FirstPollEver_SeedsFromAFullUnfilteredSweepNotA1DayLookback()
    {
        // Regression for finding #2 (docs/dev-notes.md §0a): the original first poll only looked back 1
        // day, so an Opportunity untouched in the 24 hours before the automation went live never
        // got a baseline — the real stage-change event that followed was then silently swallowed
        // as if it were the record's first-ever observation. The seed query has no
        // LastModifiedDate filter at all, so a record with an old LastModifiedDate must still be
        // seeded on the very first poll.
        var client = new Mock<ISalesforceClient>();
        client.Setup(c => c.SendAsync(DummyConnection, HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("""
                {"totalSize":1,"done":true,"records":[
                  {"attributes":{"type":"Opportunity"},"Id":"006xx0000000099","StageName":"Prospecting"}
                ]}
                """));

        var trigger = new OpportunityStageChangedTrigger(Infrastructure(), Options(), Logger<OpportunityStageChangedTrigger>());
        var context = ContextFor(
            new OpportunityStageChangedTriggerSettings(),
            SalesforcePollingState.Initial,
            client.Object,
            // The automation's very first poll happens weeks after this record's last real
            // modification — well outside the old, removed 1-day lookback window.
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await trigger.PollAsync(context, CancellationToken.None);

        result.Events.ShouldBeEmpty();
        result.NextState.Snapshot["006xx0000000099"].ShouldBe("Prospecting");
        result.NextState.LastPollUtc.ShouldBe(context.PollStartedUtc);

        client.Verify(
            c => c.SendAsync(
                DummyConnection,
                HttpMethod.Get,
                It.Is<string>(path => path.Contains("StageName", StringComparison.Ordinal) && !path.Contains("LastModifiedDate", StringComparison.Ordinal)),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OpportunityStageChangedTrigger_SeedFails_LeavesPreviousStateUntouchedSoNextPollRetries()
    {
        var client = new Mock<ISalesforceClient>();
        client.Setup(c => c.SendAsync(DummyConnection, HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalesforceApiResult
            {
                StatusCode = System.Net.HttpStatusCode.BadRequest,
                IsSuccess = false,
                Error = new SalesforceApiError("No such column 'Bogus__c' on entity 'Opportunity'.", "INVALID_FIELD", Umbraco.Automate.Core.Actions.StepRunErrorCategory.Validation),
            });

        var trigger = new OpportunityStageChangedTrigger(Infrastructure(), Options(), Logger<OpportunityStageChangedTrigger>());
        var context = ContextFor(
            new OpportunityStageChangedTriggerSettings(),
            SalesforcePollingState.Initial,
            client.Object,
            new DateTime(2026, 1, 2, 0, 5, 0, DateTimeKind.Utc));

        var result = await trigger.PollAsync(context, CancellationToken.None);

        result.Events.ShouldBeEmpty();
        result.NextState.LastPollUtc.ShouldBeNull();
        result.NextState.Snapshot.ShouldBeEmpty();
    }

    [Fact]
    public async Task OpportunityStageChangedTrigger_QueryFails_LogsWarningRatherThanFailingSilently()
    {
        // Regression: previously a failed poll query was swallowed with zero log output anywhere
        // — a misconfigured trigger (e.g. a typo'd object/field name) would silently do nothing,
        // forever, with no trace in the Run log (it never fires) or the server log.
        var client = new Mock<ISalesforceClient>();
        client.Setup(c => c.SendAsync(DummyConnection, HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalesforceApiResult
            {
                StatusCode = System.Net.HttpStatusCode.BadRequest,
                IsSuccess = false,
                Error = new SalesforceApiError("No such column 'Bogus__c' on entity 'Opportunity'.", "INVALID_FIELD", Umbraco.Automate.Core.Actions.StepRunErrorCategory.Validation),
            });

        var logger = new Mock<ILogger<OpportunityStageChangedTrigger>>();
        var trigger = new OpportunityStageChangedTrigger(Infrastructure(), Options(), logger.Object);
        var context = ContextFor(
            new OpportunityStageChangedTriggerSettings(),
            SalesforcePollingState.Initial,
            client.Object,
            new DateTime(2026, 1, 2, 0, 5, 0, DateTimeKind.Utc));

        var result = await trigger.PollAsync(context, CancellationToken.None);

        result.Events.ShouldBeEmpty();
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
