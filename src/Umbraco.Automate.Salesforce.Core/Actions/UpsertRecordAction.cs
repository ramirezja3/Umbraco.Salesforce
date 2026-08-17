using System.Net;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Creates or updates a Salesforce record by matching an External ID field — the idempotent,
/// retry-safe write path (CLAUDE.md §2 non-negotiable #10, §7). Prefer this over Create Record
/// in generated example automations.
/// </summary>
[Action("salesforce.upsertRecord", "Upsert Record",
    Description = "Creates or updates a Salesforce record, matched by an External ID field.",
    Group = "CRM",
    Icon = "icon-merge",
    ConnectionTypeAlias = "salesforce")]
public sealed class UpsertRecordAction : ActionBase<UpsertRecordSettings, UpsertRecordOutput>
{
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertRecordAction"/> class.
    /// </summary>
    public UpsertRecordAction(
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
        var settings = context.GetSettings<UpsertRecordSettings>();

        if (string.IsNullOrWhiteSpace(settings.ObjectApiName))
        {
            return ActionResult.Failed(new ArgumentException("Object is required."), StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.ExternalIdField))
        {
            return ActionResult.Failed(new ArgumentException("External Id Field is required."), StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.ExternalIdValue))
        {
            return ActionResult.Failed(new ArgumentException("External Id Value is required."), StepRunErrorCategory.Validation);
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
            $"/services/data/{apiVersion}/sobjects/{Uri.EscapeDataString(settings.ObjectApiName)}" +
            $"/{Uri.EscapeDataString(settings.ExternalIdField)}/{Uri.EscapeDataString(settings.ExternalIdValue)}",
            fields,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return SalesforceActionSupport.Failed(result);
        }

        // HTTP 201 = new record created (body has id); HTTP 204 = existing record matched and
        // updated (no body at all — see UpsertRecordOutput.RecordId doc).
        var created = result.StatusCode == HttpStatusCode.Created;
        var recordId = created && result.Json?.TryGetProperty("id", out var idElement) == true
            ? idElement.GetString()
            : null;

        return Success(new UpsertRecordOutput { RecordId = recordId, Created = created });
    }
}
