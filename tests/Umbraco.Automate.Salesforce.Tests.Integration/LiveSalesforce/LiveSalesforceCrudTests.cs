using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Persistence;
using Umbraco.Automate.Salesforce.Triggers;

namespace Umbraco.Automate.Salesforce.Tests.Integration.LiveSalesforce;

/// <summary>
/// Exercises this package's actual production code (<see cref="SalesforceClient"/>,
/// <see cref="SalesforceErrorMapper"/>, <see cref="RecordDeletedTrigger"/>) against a real
/// Salesforce org. Opt-in: every test no-ops (passes trivially) when
/// <see cref="LiveSalesforceCredentials.TryLoad"/> finds nothing configured, so this never fails
/// CI or another developer's local run — see <see cref="LiveSalesforceFixture"/>.
/// </summary>
public sealed class LiveSalesforceCrudTests : IClassFixture<LiveSalesforceFixture>
{
    private readonly LiveSalesforceFixture _fixture;

    public LiveSalesforceCrudTests(LiveSalesforceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateGetUpdateDelete_RoundTrip_AgainstRealOrg()
    {
        if (_fixture.Connection is null)
        {
            return; // Not configured locally — see LiveSalesforceCredentials.
        }

        var apiVersion = _fixture.ApiOptions.ApiVersion;
        string? leadId = null;

        try
        {
            var createResult = await _fixture.Client.SendAsync(
                _fixture.Connection, HttpMethod.Post, $"/services/data/{apiVersion}/sobjects/Lead",
                new { LastName = "AutomateIntegrationTest", Company = "AutomateIntegrationTest" },
                CancellationToken.None);

            createResult.IsSuccess.ShouldBeTrue();
            leadId = createResult.Json!.Value.GetProperty("id").GetString();
            leadId.ShouldNotBeNullOrEmpty();

            var getResult = await _fixture.Client.SendAsync(
                _fixture.Connection, HttpMethod.Get,
                $"/services/data/{apiVersion}/sobjects/Lead/{leadId}?fields=Id,LastName,Company",
                jsonBody: null, CancellationToken.None);

            getResult.IsSuccess.ShouldBeTrue();
            var fields = SalesforceJsonHelpers.ToFieldDictionary(getResult.Json!.Value);
            fields["LastName"].ShouldBe("AutomateIntegrationTest");

            var updateResult = await _fixture.Client.SendAsync(
                _fixture.Connection, HttpMethod.Patch, $"/services/data/{apiVersion}/sobjects/Lead/{leadId}",
                new { Company = "AutomateIntegrationTest_Updated" }, CancellationToken.None);

            updateResult.IsSuccess.ShouldBeTrue();

            var deleteResult = await _fixture.Client.SendAsync(
                _fixture.Connection, HttpMethod.Delete, $"/services/data/{apiVersion}/sobjects/Lead/{leadId}",
                jsonBody: null, CancellationToken.None);

            deleteResult.IsSuccess.ShouldBeTrue();
            leadId = null; // deleted — no cleanup needed
        }
        finally
        {
            if (leadId is not null)
            {
                await _fixture.Client.SendAsync(
                    _fixture.Connection, HttpMethod.Delete, $"/services/data/{apiVersion}/sobjects/Lead/{leadId}",
                    jsonBody: null, CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task CreateRecord_MissingRequiredField_MapsToRealSalesforceErrorShape()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        // Lead requires LastName and Company — omitting both should produce a real
        // REQUIRED_FIELD_MISSING error, validating SalesforceErrorMapper against Salesforce's
        // actual error response shape rather than an assumed one.
        var result = await _fixture.Client.SendAsync(
            _fixture.Connection, HttpMethod.Post, $"/services/data/{_fixture.ApiOptions.ApiVersion}/sobjects/Lead",
            new { }, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ErrorCode.ShouldBe("REQUIRED_FIELD_MISSING");
        result.Error.Category.ShouldBe(StepRunErrorCategory.Validation);
    }

    /// <summary>
    /// Exercises <see cref="RecordDeletedTrigger.PollAsync"/> against the real Deleted Records
    /// endpoint. This does <em>not</em> assert that the just-deleted record shows up — empirically,
    /// against this org, Salesforce's Deleted Records index lagged past two minutes and past its
    /// own reported <c>latestDateCovered</c> watermark on some runs, and returned it within
    /// seconds on others. That's Salesforce's own infrastructure being eventually consistent in
    /// an unpredictable way, not something this package's polling logic controls or should be
    /// tested against with a timing assertion — a flaky live test is worse than no live test.
    /// What this test does assert: the real call succeeds and parses without throwing (this is
    /// exactly the assertion that would have caught the <c>JsonElement.GetDateTime()</c> strict-parsing
    /// bug this pass found — see <c>RecordDeletedTriggerTests</c> in Tests.Unit for the fixture-based
    /// regression test, which is the reliable way this specific bug is now guarded against).
    /// </summary>
    [Fact]
    public async Task RecordDeletedTrigger_PollAsync_CallsAndParsesTheRealEndpointWithoutThrowing()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        var apiVersion = _fixture.ApiOptions.ApiVersion;
        var createResult = await _fixture.Client.SendAsync(
            _fixture.Connection, HttpMethod.Post, $"/services/data/{apiVersion}/sobjects/Lead",
            new { LastName = "AutomateIntegrationTest_DeleteDetection", Company = "AutomateIntegrationTest" },
            CancellationToken.None);
        createResult.IsSuccess.ShouldBeTrue();
        var leadId = createResult.Json!.Value.GetProperty("id").GetString()!;

        var pollStart = DateTime.UtcNow;
        var deleteResult = await _fixture.Client.SendAsync(
            _fixture.Connection, HttpMethod.Delete, $"/services/data/{apiVersion}/sobjects/Lead/{leadId}",
            jsonBody: null, CancellationToken.None);
        deleteResult.IsSuccess.ShouldBeTrue();

        var options = new Mock<IOptionsMonitor<SalesforceApiOptions>>();
        options.Setup(o => o.CurrentValue).Returns(_fixture.ApiOptions);
        var trigger = new RecordDeletedTrigger(
            new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()),
            options.Object);

        var context = new SalesforcePollingContext
        {
            AutomationId = Guid.NewGuid(),
            TriggerAlias = "salesforce.recordDeleted",
            Settings = new RecordDeletedTriggerSettings { ObjectApiName = "Lead" },
            Connection = _fixture.Connection,
            Client = _fixture.Client,
            PreviousState = new SalesforcePollingState(pollStart.AddMinutes(-1), new Dictionary<string, string>()),
            PollStartedUtc = DateTime.UtcNow,
        };

        // Must not throw — this is the actual regression coverage. Whether Events is empty or not
        // depends entirely on Salesforce's indexing timing, which this test does not assert on.
        var result = await trigger.PollAsync(context, CancellationToken.None);
        result.NextState.LastPollUtc.ShouldBe(context.PollStartedUtc);
    }
}
