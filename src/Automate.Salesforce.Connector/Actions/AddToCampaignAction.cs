using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Automate.Salesforce.Connector.Api;
using Automate.Salesforce.Connector.Configuration;
using Automate.Salesforce.Connector.Connection;

namespace Automate.Salesforce.Connector.Actions;

/// <summary>
/// Adds a Contact or Lead to a Salesforce Campaign — the "this person did the marketing thing"
/// step, e.g. completing a personalized journey, downloading gated content, or hitting a
/// segment. Requires a Salesforce connection with the <c>api</c> scope.
/// </summary>
[Action("salesforce.addToCampaign", "Add to Campaign",
    Description = "Adds a Contact or Lead to a Salesforce Campaign.",
    Group = "Salesforce",
    Icon = "icon-add",
    ConnectionTypeAlias = "salesforce")]
public sealed class AddToCampaignAction : ActionBase<AddToCampaignSettings, AddToCampaignOutput>
{
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddToCampaignAction"/> class.
    /// </summary>
    public AddToCampaignAction(
        ActionInfrastructure infrastructure,
        ISalesforceConnectionResolver connectionResolver,
        ISalesforceClient client,
        IOptionsMonitor<SalesforceApiOptions> options)
        : base(infrastructure)
    {
        _connectionResolver = connectionResolver;
        _client = client;
        _options = options;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<AddToCampaignSettings>();

        if (string.IsNullOrWhiteSpace(settings.CampaignId))
        {
            return ActionResult.Failed(new ArgumentException("Campaign Id is required."), StepRunErrorCategory.Validation);
        }

        var hasContactId = !string.IsNullOrWhiteSpace(settings.ContactId);
        var hasLeadId = !string.IsNullOrWhiteSpace(settings.LeadId);

        if (!hasContactId && !hasLeadId)
        {
            return ActionResult.Failed(
                new ArgumentException("Provide either a Contact Id or a Lead Id."), StepRunErrorCategory.Validation);
        }

        if (hasContactId && hasLeadId)
        {
            return ActionResult.Failed(
                new ArgumentException("Provide only one of Contact Id or Lead Id, not both."), StepRunErrorCategory.Validation);
        }

        if (SalesforceActionSupport.TryGetCredentialsId(context.Connection, out var credentialsId) is { } credentialFailure)
        {
            return credentialFailure;
        }

        var (connection, connectionFailure) = await SalesforceActionSupport.ResolveContextAsync(
            credentialsId, _connectionResolver, cancellationToken);
        if (connectionFailure is not null)
        {
            return connectionFailure;
        }

        var fields = new Dictionary<string, object?> { ["CampaignId"] = settings.CampaignId };

        if (hasContactId)
        {
            fields["ContactId"] = settings.ContactId;
        }
        else
        {
            fields["LeadId"] = settings.LeadId;
        }

        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            fields["Status"] = settings.Status;
        }

        var apiVersion = _options.CurrentValue.ApiVersion;
        var result = await _client.SendAsync(
            connection!,
            HttpMethod.Post,
            $"/services/data/{apiVersion}/sobjects/CampaignMember",
            fields,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return SalesforceActionSupport.Failed(result);
        }

        var recordId = result.Json?.TryGetProperty("id", out var idElement) == true ? idElement.GetString() : null;
        return Success(new AddToCampaignOutput { RecordId = recordId });
    }
}
