using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Automate.Salesforce.Connector.Actions;
using Automate.Salesforce.Connector.Api;
using Automate.Salesforce.Connector.Configuration;
using Automate.Salesforce.Connector.Connection;
using Umbraco.Automate.Testing;

namespace Automate.Salesforce.Connector.Tests.Integration.LiveSalesforce;

/// <summary>
/// Exercises the six named Action classes themselves — not just the raw REST client shapes
/// covered by <see cref="LiveSalesforceCrudTests"/> — against a real Salesforce organization, via
/// Core's <see cref="ActionTestHarness{TAction}"/> (a real <c>ConfiguredConnection</c>, real DI).
/// Opt-in and no-ops with no configured credentials — see <see cref="LiveSalesforceFixture"/>.
/// Every test cleans up whatever it creates.
/// </summary>
public sealed class LiveSalesforceActionTests : IClassFixture<LiveSalesforceFixture>
{
    private readonly LiveSalesforceFixture _fixture;

    public LiveSalesforceActionTests(LiveSalesforceFixture fixture)
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

    private async Task<Dictionary<string, object?>> GetFieldsAsync(string objectType, string id, string fields)
    {
        var result = await _fixture.Client.SendAsync(
            _fixture.Connection!, HttpMethod.Get,
            $"/services/data/{_fixture.ApiOptions.ApiVersion}/sobjects/{objectType}/{id}?fields={fields}",
            jsonBody: null, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return SalesforceJsonHelpers.ToFieldDictionary(result.Json!.Value);
    }

    [Fact]
    public async Task CreateOpportunityAction_RealOrganization_CreatesRecordWithNamedFields()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        string? opportunityId = null;
        try
        {
            var result = await Harness<CreateOpportunityAction>()
                .WithSettings(new CreateOpportunitySettings
                {
                    Name = "AutomateIntegrationTest Opportunity",
                    StageName = "Prospecting",
                    CloseDate = "2027-01-01",
                })
                .ExecuteAsync();

            result.Status.ShouldBe(ActionResultStatus.Success);
            opportunityId = ((CreateOpportunityOutput)result.OutputData!).RecordId;
            opportunityId.ShouldNotBeNullOrEmpty();

            var fields = await GetFieldsAsync("Opportunity", opportunityId!, "Name,StageName");
            fields["Name"].ShouldBe("AutomateIntegrationTest Opportunity");
            fields["StageName"].ShouldBe("Prospecting");
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
    public async Task UpdateOpportunityStageAction_RealOrganization_MovesExistingOpportunityToNewStage()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        var created = await CreateAsync("Opportunity", new
        {
            Name = "AutomateIntegrationTest Opportunity (stage update)",
            StageName = "Prospecting",
            CloseDate = "2027-01-01",
        });
        created.IsSuccess.ShouldBeTrue();
        var opportunityId = created.Json!.Value.GetProperty("id").GetString()!;

        try
        {
            var result = await Harness<UpdateOpportunityStageAction>()
                .WithSettings(new UpdateOpportunityStageSettings { OpportunityId = opportunityId, StageName = "Qualification" })
                .ExecuteAsync();

            result.Status.ShouldBe(ActionResultStatus.Success);

            var fields = await GetFieldsAsync("Opportunity", opportunityId, "StageName");
            fields["StageName"].ShouldBe("Qualification");
        }
        finally
        {
            await DeleteAsync("Opportunity", opportunityId);
        }
    }

    [Fact]
    public async Task CreateOrUpdateContactAction_RealOrganization_CreatesThenUpdatesTheSameRecord()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        string? contactId = null;
        try
        {
            var createResult = await Harness<CreateOrUpdateContactAction>()
                .WithSettings(new CreateOrUpdateContactSettings { LastName = "AutomateIntegrationTest" })
                .ExecuteAsync();

            createResult.Status.ShouldBe(ActionResultStatus.Success);
            var createOutput = (CreateOrUpdateContactOutput)createResult.OutputData!;
            createOutput.Created.ShouldBeTrue();
            contactId = createOutput.RecordId;
            contactId.ShouldNotBeNullOrEmpty();

            var updateResult = await Harness<CreateOrUpdateContactAction>()
                .WithSettings(new CreateOrUpdateContactSettings { LastName = "AutomateIntegrationTest_Updated", ContactId = contactId })
                .ExecuteAsync();

            updateResult.Status.ShouldBe(ActionResultStatus.Success);
            var updateOutput = (CreateOrUpdateContactOutput)updateResult.OutputData!;
            updateOutput.Created.ShouldBeFalse();
            updateOutput.RecordId.ShouldBe(contactId);

            var fields = await GetFieldsAsync("Contact", contactId!, "LastName");
            fields["LastName"].ShouldBe("AutomateIntegrationTest_Updated");
        }
        finally
        {
            if (contactId is not null)
            {
                await DeleteAsync("Contact", contactId);
            }
        }
    }

    [Fact]
    public async Task AddToCampaignAction_RealOrganization_CreatesCampaignMemberForALead()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        var campaign = await CreateAsync("Campaign", new { Name = "AutomateIntegrationTest Campaign", IsActive = true });
        campaign.IsSuccess.ShouldBeTrue(campaign.Error?.Message + " | " + campaign.Error?.ErrorCode);
        var campaignId = campaign.Json!.Value.GetProperty("id").GetString()!;

        var lead = await CreateAsync("Lead", new { LastName = "AutomateIntegrationTest", Company = "AutomateIntegrationTest" });
        lead.IsSuccess.ShouldBeTrue();
        var leadId = lead.Json!.Value.GetProperty("id").GetString()!;

        string? campaignMemberId = null;
        try
        {
            var result = await Harness<AddToCampaignAction>()
                .WithSettings(new AddToCampaignSettings { CampaignId = campaignId, LeadId = leadId })
                .ExecuteAsync();

            result.Status.ShouldBe(ActionResultStatus.Success);
            campaignMemberId = ((AddToCampaignOutput)result.OutputData!).RecordId;
            campaignMemberId.ShouldNotBeNullOrEmpty();

            var fields = await GetFieldsAsync("CampaignMember", campaignMemberId!, "CampaignId,LeadId");
            fields["CampaignId"].ShouldBe(campaignId);
            fields["LeadId"].ShouldBe(leadId);
        }
        finally
        {
            if (campaignMemberId is not null)
            {
                await DeleteAsync("CampaignMember", campaignMemberId);
            }

            await DeleteAsync("Lead", leadId);
            await DeleteAsync("Campaign", campaignId);
        }
    }

    [Fact]
    public async Task LogEngagementActivityAction_RealOrganization_CreatesCompletedTaskForALead()
    {
        if (_fixture.Connection is null)
        {
            return;
        }

        var lead = await CreateAsync("Lead", new { LastName = "AutomateIntegrationTest", Company = "AutomateIntegrationTest" });
        lead.IsSuccess.ShouldBeTrue();
        var leadId = lead.Json!.Value.GetProperty("id").GetString()!;

        string? taskId = null;
        try
        {
            var result = await Harness<LogEngagementActivityAction>()
                .WithSettings(new LogEngagementActivitySettings { WhoId = leadId, Subject = "AutomateIntegrationTest Engagement" })
                .ExecuteAsync();

            result.Status.ShouldBe(ActionResultStatus.Success);
            taskId = ((LogEngagementActivityOutput)result.OutputData!).RecordId;
            taskId.ShouldNotBeNullOrEmpty();

            var fields = await GetFieldsAsync("Task", taskId!, "Subject,Status,WhoId");
            fields["Subject"].ShouldBe("AutomateIntegrationTest Engagement");
            fields["Status"].ShouldBe("Completed");
            fields["WhoId"].ShouldBe(leadId);
        }
        finally
        {
            if (taskId is not null)
            {
                await DeleteAsync("Task", taskId);
            }

            await DeleteAsync("Lead", leadId);
        }
    }
}
