using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Creates a record of a configured Salesforce object type with mapped field values.
/// Requires a Salesforce connection with the <c>api</c> scope.
/// </summary>
[Action("salesforce.createRecord", "Create Record",
    Description = "Creates a Salesforce record of a configured object type with mapped field values.",
    Group = "Salesforce",
    Icon = "icon-add",
    ConnectionTypeAlias = "salesforce")]
public sealed class CreateRecordAction : ActionBase<CreateRecordSettings, CreateRecordOutput>
{
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateRecordAction"/> class.
    /// </summary>
    public CreateRecordAction(
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
        var settings = context.GetSettings<CreateRecordSettings>();

        if (string.IsNullOrWhiteSpace(settings.ObjectApiName))
        {
            return ActionResult.Failed(new ArgumentException("Object is required."), StepRunErrorCategory.Validation);
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
            HttpMethod.Post,
            $"/services/data/{apiVersion}/sobjects/{Uri.EscapeDataString(settings.ObjectApiName)}",
            fields,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return SalesforceActionSupport.Failed(result);
        }

        var recordId = result.Json?.TryGetProperty("id", out var idElement) == true ? idElement.GetString() : null;
        return Success(new CreateRecordOutput { RecordId = recordId });
    }
}
