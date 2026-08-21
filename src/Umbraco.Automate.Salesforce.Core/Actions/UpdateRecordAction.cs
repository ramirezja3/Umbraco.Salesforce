using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Updates a Salesforce record by ID with mapped field values.
/// Requires a Salesforce connection with the <c>api</c> scope.
/// </summary>
[Action("salesforce.updateRecord", "Update Record",
    Description = "Updates a Salesforce record by ID with mapped field values.",
    Group = "Salesforce",
    Icon = "icon-edit",
    ConnectionTypeAlias = "salesforce")]
public sealed class UpdateRecordAction : ActionBase<UpdateRecordSettings, UpdateRecordOutput>
{
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateRecordAction"/> class.
    /// </summary>
    public UpdateRecordAction(
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
        var settings = context.GetSettings<UpdateRecordSettings>();

        if (string.IsNullOrWhiteSpace(settings.ObjectApiName))
        {
            return ActionResult.Failed(new ArgumentException("Object is required."), StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.RecordId))
        {
            return ActionResult.Failed(new ArgumentException("Record Id is required."), StepRunErrorCategory.Validation);
        }

        if (SalesforceActionSupport.TryGetCredentialsId(context.Connection, out var credentialsId) is { } credentialFailure)
        {
            return credentialFailure;
        }

        if (SalesforceActionSupport.TryParseFields(settings.Fields, out var fields) is { } parseFailure)
        {
            return parseFailure;
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
            $"/services/data/{apiVersion}/sobjects/{Uri.EscapeDataString(settings.ObjectApiName)}/{Uri.EscapeDataString(settings.RecordId)}",
            fields,
            cancellationToken);

        // Salesforce returns 204 No Content on a successful update.
        if (!result.IsSuccess)
        {
            return SalesforceActionSupport.Failed(result);
        }

        return Success(new UpdateRecordOutput { RecordId = settings.RecordId });
    }
}
