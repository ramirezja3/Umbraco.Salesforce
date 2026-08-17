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

/// <summary>
/// Regression coverage for a real bug caught only by live-org testing (see CLAUDE.md §0a):
/// Salesforce's Deleted Records endpoint emits <c>deletedDate</c> as <c>+0000</c> (no colon in
/// the UTC offset), which <see cref="JsonElement.GetDateTime"/>'s strict RFC 3339 parser rejects.
/// This fixture uses that exact real-world format on purpose so it would have caught the bug
/// without needing a live org, had it existed from the start.
/// </summary>
public class RecordDeletedTriggerTests
{
    private static readonly SalesforceConnectionContext DummyConnection =
        new("token", new Uri("https://example.my.salesforce.com"));

    [Fact]
    public async Task PollAsync_SalesforceOffsetFormatWithoutColon_ParsesWithoutThrowing()
    {
        var client = new Mock<ISalesforceClient>();
        client.Setup(c => c.SendAsync(DummyConnection, HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalesforceApiResult
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                IsSuccess = true,
                Json = JsonDocument.Parse("""
                    {"deletedRecords":[{"id":"00QgK00000QFdjdUAD","deletedDate":"2026-08-17T20:12:59.000+0000"}],
                     "earliestDateAvailable":"2026-08-07T18:06:00.000+0000","latestDateCovered":"2026-08-17T20:13:00.000+0000"}
                    """).RootElement,
            });

        var optionsMonitor = new Mock<IOptionsMonitor<SalesforceApiOptions>>();
        optionsMonitor.Setup(o => o.CurrentValue).Returns(new SalesforceApiOptions());
        var trigger = new RecordDeletedTrigger(new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()), optionsMonitor.Object);

        var context = new SalesforcePollingContext
        {
            AutomationId = Guid.NewGuid(),
            TriggerAlias = "salesforce.recordDeleted",
            Settings = new RecordDeletedTriggerSettings { ObjectApiName = "Lead" },
            Connection = DummyConnection,
            Client = client.Object,
            PreviousState = new SalesforcePollingState(new DateTime(2026, 8, 17, 19, 0, 0, DateTimeKind.Utc), new Dictionary<string, string>()),
            PollStartedUtc = new DateTime(2026, 8, 17, 20, 15, 0, DateTimeKind.Utc),
        };

        var result = await trigger.PollAsync(context, CancellationToken.None);

        result.Events.Count.ShouldBe(1);
        var output = ((TriggerEvent<RecordDeletedTriggerOutput>)result.Events[0]).Output;
        output.RecordId.ShouldBe("00QgK00000QFdjdUAD");
        output.DeletedDateUtc.ShouldBe(new DateTime(2026, 8, 17, 20, 12, 59, DateTimeKind.Utc));
    }
}
