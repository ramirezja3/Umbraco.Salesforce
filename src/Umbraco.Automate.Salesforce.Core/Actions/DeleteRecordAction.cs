using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Deletes a Salesforce record by ID. Destructive — requires <see cref="DeleteRecordSettings.ConfirmDelete"/>
/// to be explicitly set (docs/dev-notes.md §7/§8).
/// </summary>
[Action("salesforce.deleteRecord", "Delete Record",
    Description = "Deletes a Salesforce record by ID. Destructive — cannot be undone.",
    Group = "Salesforce",
    Icon = "icon-trash",
    ConnectionTypeAlias = "salesforce")]
public sealed class DeleteRecordAction : ActionBase<DeleteRecordSettings, DeleteRecordOutput>
{
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteRecordAction"/> class.
    /// </summary>
    public DeleteRecordAction(
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
        var settings = context.GetSettings<DeleteRecordSettings>();

        if (string.IsNullOrWhiteSpace(settings.ObjectApiName))
        {
            return ActionResult.Failed(new ArgumentException("Object is required."), StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.RecordId))
        {
            return ActionResult.Failed(new ArgumentException("Record Id is required."), StepRunErrorCategory.Validation);
        }

        if (!settings.ConfirmDelete)
        {
            return ActionResult.Failed(
                new InvalidOperationException("Confirm Delete must be checked to run this destructive action."),
                StepRunErrorCategory.Validation);
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
            HttpMethod.Delete,
            $"/services/data/{apiVersion}/sobjects/{Uri.EscapeDataString(settings.ObjectApiName)}/{Uri.EscapeDataString(settings.RecordId)}",
            jsonBody: null,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return SalesforceActionSupport.Failed(result);
        }

        return Success(new DeleteRecordOutput { RecordId = settings.RecordId });
    }
}
