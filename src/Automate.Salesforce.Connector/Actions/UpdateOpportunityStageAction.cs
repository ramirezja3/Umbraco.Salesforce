using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Automate.Salesforce.Connector.Api;
using Automate.Salesforce.Connector.Configuration;
using Automate.Salesforce.Connector.Connection;

namespace Automate.Salesforce.Connector.Actions;

/// <summary>
/// Moves an existing Salesforce Opportunity to a new stage — e.g. marking it "Closed Won" when a
/// Commerce order ships, or "Closed Lost" when one is cancelled or refunded. Requires a
/// Salesforce connection with the <c>api</c> scope.
/// </summary>
[Action("salesforce.updateOpportunityStage", "Update Opportunity Stage",
    Description = "Moves an existing Salesforce Opportunity to a new stage.",
    Group = "Salesforce",
    Icon = "icon-trending",
    ConnectionTypeAlias = "salesforce")]
public sealed class UpdateOpportunityStageAction : ActionBase<UpdateOpportunityStageSettings, UpdateOpportunityStageOutput>
{
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOpportunityStageAction"/> class.
    /// </summary>
    public UpdateOpportunityStageAction(
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
        var settings = context.GetSettings<UpdateOpportunityStageSettings>();

        if (string.IsNullOrWhiteSpace(settings.OpportunityId))
        {
            return ActionResult.Failed(new ArgumentException("Opportunity Id is required."), StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.StageName))
        {
            return ActionResult.Failed(new ArgumentException("Stage is required."), StepRunErrorCategory.Validation);
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

        var apiVersion = _options.CurrentValue.ApiVersion;
        var result = await _client.SendAsync(
            connection!,
            HttpMethod.Patch,
            $"/services/data/{apiVersion}/sobjects/Opportunity/{Uri.EscapeDataString(settings.OpportunityId)}",
            new { StageName = settings.StageName },
            cancellationToken);

        // Salesforce returns 204 No Content on a successful update.
        if (!result.IsSuccess)
        {
            return SalesforceActionSupport.Failed(result);
        }

        return Success(new UpdateOpportunityStageOutput { RecordId = settings.OpportunityId });
    }
}
