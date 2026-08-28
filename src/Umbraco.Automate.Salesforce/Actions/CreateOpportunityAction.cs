using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Creates a Salesforce Opportunity with named fields for the common attributes — the "log a
/// deal" step, e.g. when a Commerce order is placed. Requires a Salesforce connection with the
/// <c>api</c> scope.
/// </summary>
[Action("salesforce.createOpportunity", "Create Opportunity",
    Description = "Creates a Salesforce Opportunity with named fields for the common attributes.",
    Group = "Salesforce",
    Icon = "icon-add",
    ConnectionTypeAlias = "salesforce")]
public sealed class CreateOpportunityAction : ActionBase<CreateOpportunitySettings, CreateOpportunityOutput>
{
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOpportunityAction"/> class.
    /// </summary>
    public CreateOpportunityAction(
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
        var settings = context.GetSettings<CreateOpportunitySettings>();

        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            return ActionResult.Failed(new ArgumentException("Name is required."), StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.StageName))
        {
            return ActionResult.Failed(new ArgumentException("Stage is required."), StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.CloseDate))
        {
            return ActionResult.Failed(new ArgumentException("Close Date is required."), StepRunErrorCategory.Validation);
        }

        if (SalesforceActionSupport.TryGetCredentialsId(context.Connection, out var credentialsId) is { } credentialFailure)
        {
            return credentialFailure;
        }

        if (SalesforceActionSupport.TryParseFields(settings.AdditionalFields, out var fields) is { } parseFailure)
        {
            return parseFailure;
        }

        var (connection, connectionFailure) = await SalesforceActionSupport.ResolveContextAsync(
            credentialsId, _connectionResolver, cancellationToken);
        if (connectionFailure is not null)
        {
            return connectionFailure;
        }

        fields["Name"] = settings.Name;
        fields["StageName"] = settings.StageName;
        fields["CloseDate"] = settings.CloseDate;

        if (!string.IsNullOrWhiteSpace(settings.AccountId))
        {
            fields["AccountId"] = settings.AccountId;
        }

        if (settings.Amount is { } amount)
        {
            fields["Amount"] = amount;
        }

        var apiVersion = _options.CurrentValue.ApiVersion;
        var result = await _client.SendAsync(
            connection!,
            HttpMethod.Post,
            $"/services/data/{apiVersion}/sobjects/Opportunity",
            fields,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return SalesforceActionSupport.Failed(result);
        }

        var recordId = result.Json?.TryGetProperty("id", out var idElement) == true ? idElement.GetString() : null;
        return Success(new CreateOpportunityOutput { RecordId = recordId });
    }
}
