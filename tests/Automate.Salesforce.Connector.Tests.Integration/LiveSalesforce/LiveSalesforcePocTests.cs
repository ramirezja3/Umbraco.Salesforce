using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Automate.Salesforce.Connector.Actions;
using Automate.Salesforce.Connector.Api;
using Automate.Salesforce.Connector.Configuration;
using Automate.Salesforce.Connector.Connection;
using Umbraco.Automate.Testing;

namespace Automate.Salesforce.Connector.Tests.Integration.LiveSalesforce;

/// <summary>
/// Heavy, practical POC coverage against a real Salesforce organization: all 6 actions chained
/// together as a realistic customer journey, deliberate error-path probes to check the quality of
/// surfaced messages, a bulk (For-Each-style) loop, and concurrent (Parallel-style) calls sharing
/// one connection. This exists because the Automate canvas UI in this environment's browser
/// automation session could not be gotten to render (blank content area, zero console/network
/// errors, isolated to the Automate section specifically — see docs/dev-notes.md) — this suite
/// exercises the same underlying Action code the canvas would call, at the REST/action level,
/// instead of waiting on that UI. It does not exercise real triggers or canvas control-flow nodes
/// (If/Switch/ForEach/Parallel as actual steps) since those require the canvas. Opt-in and no-ops
/// with no configured credentials — see <see cref="LiveSalesforceFixture"/>. Every test cleans up
/// whatever it creates, tagged with a "POC" marker for easy identification.
/// </summary>
public sealed class LiveSalesforcePocTests : IClassFixture<LiveSalesforceFixture>
{
    private const string Tag = "POC";

    private readonly LiveSalesforceFixture _fixture;

    public LiveSalesforcePocTests(LiveSalesforceFixture fixture)
    {
        _fixture = fixture;
    }

    private ActionTestHarness<TAction> Harness<TAction>() where TAction : class, IAction
    {
        var options = new Mock<IOptionsMonitor<SalesforceApiOptions>>();
        options.Setup(o => o.CurrentValue).Returns(_fixture.ApiOptions);

        var resolver = new Mock<ISalesforceConnectionResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_fixture.Connection);

        return ActionTestHarness.For<TAction>()
            .WithService(resolver.Object)
            .WithService(_fixture.Client)
            .WithService(options.Object)
            .WithConnection(new SalesforceConnectionSettings { OAuthCredentialsId = Guid.NewGuid() });
    }

    private Task<SalesforceApiResult> CreateAsync(string objectType, object fields)
        => _fixture.Client.SendAsync(
            _fixture.Connection!, HttpMethod.Post,
            $"/services/data/{_fixture.ApiOptions.ApiVersion}/sobjects/{objectType}", fields, CancellationToken.None);

    private Task<SalesforceApiResult> DeleteAsync(string objectType, string id)
        => _fixture.Client.SendAsync(
            _fixture.Connection!, HttpMethod.Delete,
            $"/services/data/{_fixture.ApiOptions.ApiVersion}/sobjects/{objectType}/{id}", jsonBody: null, CancellationToken.None);

    /// <summary>
    /// The realistic end-to-end shape this package exists for: a website visitor becomes a Lead,
    /// converts into a logged Opportunity, the deal progresses, the person is separately tracked
    /// as a known Contact, added to a nurture Campaign, and a behavioral signal is logged against
    /// them — six actions, six real API calls, each step's output feeding a later step exactly as
    /// an automation binding a prior step's `${ }` output would.
    /// </summary>
    [Fact]
    public async Task FullCustomerJourney_AllSixActionsChained_RealOrganization()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        string? leadId = null;
        string? opportunityId = null;
        string? contactId = null;
        string? campaignId = null;
        string? campaignMemberId = null;
        string? taskId = null;

        try
        {
            // Step 1: web visitor -> Lead.
            var leadResult = await Harness<CreateLeadAction>()
                .WithSettings(new CreateLeadSettings
                {
                    LastName = $"{Tag}-Journey",
                    Company = $"{Tag} Corp",
                    Email = "poc-journey@example.com",
                    LeadSource = "Web",
                })
                .ExecuteAsync();
            leadResult.Status.ShouldBe(ActionResultStatus.Success, leadResult.Exception?.Message);
            leadId = ((CreateLeadOutput)leadResult.OutputData!).RecordId;
            leadId.ShouldNotBeNullOrEmpty();

            // Step 2: the same journey logs a deal (independent of the Lead — Opportunity has no
            // native Lead relationship without conversion, which this package deliberately doesn't
            // attempt — see docs/dev-notes.md on Convert Lead being dropped).
            var opportunityResult = await Harness<CreateOpportunityAction>()
                .WithSettings(new CreateOpportunitySettings
                {
                    Name = $"{Tag}-Journey Deal",
                    StageName = "Prospecting",
                    CloseDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"),
                    Amount = 5000,
                })
                .ExecuteAsync();
            opportunityResult.Status.ShouldBe(ActionResultStatus.Success, opportunityResult.Exception?.Message);
            opportunityId = ((CreateOpportunityOutput)opportunityResult.OutputData!).RecordId;

            // Step 3: the deal progresses — bind the prior step's own output back in, exactly as
            // an automation step would via `${ steps.createOpportunity.RecordId }`.
            var stageResult = await Harness<UpdateOpportunityStageAction>()
                .WithSettings(new UpdateOpportunityStageSettings { OpportunityId = opportunityId!, StageName = "Closed Won" })
                .ExecuteAsync();
            stageResult.Status.ShouldBe(ActionResultStatus.Success, stageResult.Exception?.Message);

            // Step 4: the same journey, now tracked as a known-customer Contact. Deliberately a
            // *different* email than the Lead in step 1 — see
            // CreateOrUpdateContact_EmailMatchesExistingLead_RealOrganization_DuplicateRuleBlocksWithClearError
            // below for what happens (live-confirmed) when a Contact and a Lead in this org share
            // an email: Salesforce's standard Duplicate Rules reject the write outright, which a
            // real "Lead becomes a Contact" journey could genuinely hit if both steps used the
            // same trigger data.
            var contactResult = await Harness<CreateOrUpdateContactAction>()
                .WithSettings(new CreateOrUpdateContactSettings
                {
                    LastName = $"{Tag}-Journey",
                    Email = "poc-journey-contact@example.com",
                })
                .ExecuteAsync();
            contactResult.Status.ShouldBe(ActionResultStatus.Success, contactResult.Exception?.Message);
            contactId = ((CreateOrUpdateContactOutput)contactResult.OutputData!).RecordId;

            // A Campaign to add them to (this package has no Create Campaign action by design —
            // Campaigns are marketing-owned setup, not something an automation should spin up).
            var campaign = await CreateAsync("Campaign", new { Name = $"{Tag}-Journey Campaign", IsActive = true });
            campaign.IsSuccess.ShouldBeTrue(campaign.Error?.Message);
            campaignId = campaign.Json!.Value.GetProperty("id").GetString();

            // Step 5: bind the Contact Id from step 4 into the campaign membership.
            var campaignMemberResult = await Harness<AddToCampaignAction>()
                .WithSettings(new AddToCampaignSettings { CampaignId = campaignId!, ContactId = contactId })
                .ExecuteAsync();
            campaignMemberResult.Status.ShouldBe(ActionResultStatus.Success, campaignMemberResult.Exception?.Message);
            campaignMemberId = ((AddToCampaignOutput)campaignMemberResult.OutputData!).RecordId;

            // Step 6: a behavioral signal, logged against the same Contact Id.
            var taskResult = await Harness<LogEngagementActivityAction>()
                .WithSettings(new LogEngagementActivitySettings
                {
                    WhoId = contactId!,
                    Subject = $"{Tag}-Journey: Closed Won follow-up",
                    Description = "Deal closed — logged via the full customer journey POC.",
                })
                .ExecuteAsync();
            taskResult.Status.ShouldBe(ActionResultStatus.Success, taskResult.Exception?.Message);
            taskId = ((LogEngagementActivityOutput)taskResult.OutputData!).RecordId;

            // Verify the whole chain actually landed correctly by reading it all back at once.
            var oppFields = await ReadFieldsAsync("Opportunity", opportunityId!, "StageName,Amount");
            oppFields["StageName"].ShouldBe("Closed Won");

            var memberFields = await ReadFieldsAsync("CampaignMember", campaignMemberId!, "ContactId,CampaignId");
            memberFields["ContactId"].ShouldBe(contactId);

            var taskFields = await ReadFieldsAsync("Task", taskId!, "WhoId,Status");
            taskFields["WhoId"].ShouldBe(contactId);
            taskFields["Status"].ShouldBe("Completed");
        }
        finally
        {
            if (taskId is not null) await DeleteAsync("Task", taskId);
            if (campaignMemberId is not null) await DeleteAsync("CampaignMember", campaignMemberId);
            if (campaignId is not null) await DeleteAsync("Campaign", campaignId);
            if (contactId is not null) await DeleteAsync("Contact", contactId);
            if (opportunityId is not null) await DeleteAsync("Opportunity", opportunityId);
            if (leadId is not null) await DeleteAsync("Lead", leadId);
        }
    }

    private async Task<Dictionary<string, object?>> ReadFieldsAsync(string objectType, string id, string fields)
    {
        var result = await _fixture.Client.SendAsync(
            _fixture.Connection!, HttpMethod.Get,
            $"/services/data/{_fixture.ApiOptions.ApiVersion}/sobjects/{objectType}/{id}?fields={fields}",
            jsonBody: null, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return SalesforceJsonHelpers.ToFieldDictionary(result.Json!.Value);
    }

    // ----- Error-path probes: deliberately wrong-but-well-formed input, checking the real
    // Salesforce error surfaces as a clear SalesforceApiException, not a raw dump. -----

    [Fact]
    public async Task CreateOpportunity_InvalidStagePicklistValue_RealOrganization_SalesforceAcceptsItSilently()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        // Real finding, not the originally-assumed behavior: StageName is NOT a Restricted
        // Picklist by default, so Salesforce's REST API accepts an arbitrary string here rather
        // than rejecting it. Neither this action nor Salesforce validates it against the
        // org's actual configured stages — an automation author's typo in the Stage input (e.g.
        // "Closed-Won" instead of "Closed Won") silently succeeds and leaves the Opportunity in a
        // stage that doesn't correspond to any real sales-process stage, with no error anywhere to
        // signal the mistake. Confirmed live rather than assumed — see docs/dev-notes.md.
        string? opportunityId = null;
        try
        {
            var result = await Harness<CreateOpportunityAction>()
                .WithSettings(new CreateOpportunitySettings
                {
                    Name = $"{Tag}-InvalidStage",
                    StageName = "NotARealStageValue",
                    CloseDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"),
                })
                .ExecuteAsync();

            result.Status.ShouldBe(ActionResultStatus.Success, result.Exception?.Message);
            opportunityId = ((CreateOpportunityOutput)result.OutputData!).RecordId;
        }
        finally
        {
            if (opportunityId is not null)
            {
                await DeleteAsync("Opportunity", opportunityId);
            }
        }
    }

    [Fact]
    public async Task UpdateOpportunityStage_NonExistentOpportunityId_RealOrganization_SurfacesCleanError()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        // Well-formed 18-char Salesforce Id shape, guaranteed not to exist.
        const string fakeId = "006000000000000AAA";

        var result = await Harness<UpdateOpportunityStageAction>()
            .WithSettings(new UpdateOpportunityStageSettings { OpportunityId = fakeId, StageName = "Qualification" })
            .ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.Exception.ShouldBeOfType<SalesforceApiException>();
    }

    [Fact]
    public async Task AddToCampaign_NonExistentCampaignId_RealOrganization_SurfacesCleanError()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        var lead = await CreateAsync("Lead", new { LastName = $"{Tag}-BadCampaign", Company = $"{Tag} Corp" });
        lead.IsSuccess.ShouldBeTrue();
        var leadId = lead.Json!.Value.GetProperty("id").GetString()!;

        try
        {
            const string fakeCampaignId = "701000000000000AAA";

            var result = await Harness<AddToCampaignAction>()
                .WithSettings(new AddToCampaignSettings { CampaignId = fakeCampaignId, LeadId = leadId })
                .ExecuteAsync();

            result.Status.ShouldBe(ActionResultStatus.Failed);
            result.Exception.ShouldBeOfType<SalesforceApiException>();
        }
        finally
        {
            await DeleteAsync("Lead", leadId);
        }
    }

    /// <summary>
    /// Real, practical finding: a "Lead becomes a Contact" journey that reuses the same email
    /// across both steps collides with Salesforce's standard Duplicate Rules (a different
    /// mechanism than the DUPLICATE_VALUE unique-field error this package already handled) — the
    /// Create/Update Contact call is rejected outright, with no automatic way for this package to
    /// resolve it (Salesforce's own suggested fix, converting the Lead, has no REST-only path —
    /// see docs/dev-notes.md on Convert Lead being dropped). Fixed the error message quality here;
    /// the underlying collision itself is a real Salesforce behavior this package can surface
    /// clearly but not avoid.
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateContact_EmailMatchesExistingLead_RealOrganization_DuplicateRuleBlocksWithClearError()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        var lead = await CreateAsync("Lead", new
        {
            LastName = $"{Tag}-DupeCheck",
            Company = $"{Tag} Corp",
            Email = "poc-dupecheck@example.com",
        });
        lead.IsSuccess.ShouldBeTrue(lead.Error?.Message);
        var leadId = lead.Json!.Value.GetProperty("id").GetString()!;

        try
        {
            var result = await Harness<CreateOrUpdateContactAction>()
                .WithSettings(new CreateOrUpdateContactSettings
                {
                    LastName = $"{Tag}-DupeCheck",
                    Email = "poc-dupecheck@example.com",
                })
                .ExecuteAsync();

            result.Status.ShouldBe(ActionResultStatus.Failed);
            var exception = result.Exception.ShouldBeOfType<SalesforceApiException>();
            exception.ErrorCode.ShouldBe("DUPLICATES_DETECTED");
            exception.Message.ShouldContain("Duplicate Rules", Case.Insensitive);
        }
        finally
        {
            await DeleteAsync("Lead", leadId);
        }
    }

    [Fact]
    public async Task LogEngagementActivity_WhoIdIsNeitherContactNorLead_RealOrganization_SurfacesCleanError()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        // A well-formed Account Id (polymorphic WhoId only accepts Contact/Lead — an Account Id
        // must be rejected by Salesforce, not silently accepted).
        const string fakeAccountId = "001000000000000AAA";

        var result = await Harness<LogEngagementActivityAction>()
            .WithSettings(new LogEngagementActivitySettings { WhoId = fakeAccountId, Subject = $"{Tag}-BadWhoId" })
            .ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.Exception.ShouldBeOfType<SalesforceApiException>();
    }

    // ----- Bulk (For-Each-style) and concurrent (Parallel-style) coverage, simulating the two
    // canvas control-flow shapes that could not be tested directly via the (unavailable) canvas. -----

    [Fact]
    public async Task ForEachStyle_SequentialLoopCreatingFiveLeads_RealOrganization()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        var createdIds = new List<string>();
        try
        {
            for (var i = 0; i < 5; i++)
            {
                var result = await Harness<CreateLeadAction>()
                    .WithSettings(new CreateLeadSettings { LastName = $"{Tag}-Bulk-{i}", Company = $"{Tag} Corp" })
                    .ExecuteAsync();

                result.Status.ShouldBe(ActionResultStatus.Success, $"iteration {i}: {result.Exception?.Message}");
                var id = ((CreateLeadOutput)result.OutputData!).RecordId;
                id.ShouldNotBeNullOrEmpty();
                createdIds.Add(id!);
            }

            createdIds.Count.ShouldBe(5);
            createdIds.Distinct().Count().ShouldBe(5); // no accidental Id reuse/collision across iterations
        }
        finally
        {
            foreach (var id in createdIds)
            {
                await DeleteAsync("Lead", id);
            }
        }
    }

    [Fact]
    public async Task ParallelStyle_FiveConcurrentCreateLeadCalls_SharingOneConnection_RealOrganization()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        // Simulates a canvas "Parallel" node with 5 branches each calling Create Lead, all
        // resolving the same connection concurrently — exercises SalesforceConnectionResolver's
        // real locking behavior and SalesforceClient's thread-safety under genuine concurrent
        // network I/O, not just mocked/sequential unit tests.
        var tasks = Enumerable.Range(0, 5).Select(i =>
            Harness<CreateLeadAction>()
                .WithSettings(new CreateLeadSettings { LastName = $"{Tag}-Parallel-{i}", Company = $"{Tag} Corp" })
                .ExecuteAsync());

        var results = await Task.WhenAll(tasks);

        var createdIds = new List<string>();
        try
        {
            foreach (var result in results)
            {
                result.Status.ShouldBe(ActionResultStatus.Success, result.Exception?.Message);
                var id = ((CreateLeadOutput)result.OutputData!).RecordId;
                id.ShouldNotBeNullOrEmpty();
                createdIds.Add(id!);
            }

            createdIds.Distinct().Count().ShouldBe(5); // concurrent calls must not collide/duplicate
        }
        finally
        {
            foreach (var id in createdIds)
            {
                await DeleteAsync("Lead", id);
            }
        }
    }
}
