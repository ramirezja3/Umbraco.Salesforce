using System.Net;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Retrieves a Salesforce record by ID for use in later automation steps.
/// Requires a Salesforce connection with the <c>api</c> scope.
/// </summary>
[Action("salesforce.getRecord", "Get Record",
    Description = "Retrieves a Salesforce record by ID.",
    Group = "CRM",
    Icon = "icon-search",
    ConnectionTypeAlias = "salesforce")]
public sealed class GetRecordAction : ActionBase<GetRecordSettings, GetRecordOutput>
{
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetRecordAction"/> class.
    /// </summary>
    public GetRecordAction(
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
        var settings = context.GetSettings<GetRecordSettings>();

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

        var (connection, connectionFailure) = await SalesforceActionSupport.ResolveContextAsync(
            credentialsId, _connectionResolver, cancellationToken);
        if (connectionFailure is not null)
        {
            return connectionFailure;
        }

        var apiVersion = _options.CurrentValue.ApiVersion;
        var path = $"/services/data/{apiVersion}/sobjects/{Uri.EscapeDataString(settings.ObjectApiName)}/{Uri.EscapeDataString(settings.RecordId)}";
        if (!string.IsNullOrWhiteSpace(settings.Fields))
        {
            path += $"?fields={Uri.EscapeDataString(settings.Fields)}";
        }

        var result = await _client.SendAsync(connection!, HttpMethod.Get, path, jsonBody: null, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.StatusCode == HttpStatusCode.NotFound)
            {
                return Success(new GetRecordOutput { Found = false });
            }

            return SalesforceActionSupport.Failed(result);
        }

        var fields = result.Json is { } json ? SalesforceJsonHelpers.ToFieldDictionary(json) : [];
        return Success(new GetRecordOutput { Found = true, Fields = fields });
    }
}
