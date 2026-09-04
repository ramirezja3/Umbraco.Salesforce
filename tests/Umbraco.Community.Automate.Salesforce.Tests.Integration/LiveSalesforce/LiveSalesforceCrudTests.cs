using Umbraco.Automate.Core.Actions;
using Umbraco.Community.Automate.Salesforce.Api;

namespace Umbraco.Community.Automate.Salesforce.Tests.Integration.LiveSalesforce;

/// <summary>
/// Exercises this package's actual production code (<see cref="SalesforceClient"/>,
/// <see cref="SalesforceErrorMapper"/>) against a real Salesforce organization. Opt-in: every
/// test no-ops (passes trivially) when <see cref="LiveSalesforceCredentials.TryLoad"/> finds
/// nothing configured, so this never fails CI or another developer's local run — see
/// <see cref="LiveSalesforceFixture"/>.
/// </summary>
public sealed class LiveSalesforceCrudTests : IClassFixture<LiveSalesforceFixture>
{
    private readonly LiveSalesforceFixture _fixture;

    public LiveSalesforceCrudTests(LiveSalesforceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateGetUpdateDelete_RoundTrip_AgainstRealOrganization()
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
}
