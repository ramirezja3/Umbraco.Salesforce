using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Configuration;
using Umbraco.Automate.Salesforce.Connection;

namespace Umbraco.Automate.Salesforce.Actions;

/// <summary>
/// Creates a Salesforce Lead with named fields for the common attributes, rather than the generic
/// <see cref="CreateRecordAction"/>'s raw JSON field map — the classic "web form to CRM" step
/// (docs/dev-notes.md §11). Requires a Salesforce connection with the <c>api</c> scope.
/// </summary>
[Action("salesforce.createLead", "Create Lead",
    Description = "Creates a Salesforce Lead with named fields for the common attributes.",
    Group = "Salesforce",
    Icon = "icon-add",
    ConnectionTypeAlias = "salesforce")]
public sealed class CreateLeadAction : ActionBase<CreateLeadSettings, CreateLeadOutput>
{
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateLeadAction"/> class.
    /// </summary>
    public CreateLeadAction(
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
        var settings = context.GetSettings<CreateLeadSettings>();

        if (string.IsNullOrWhiteSpace(settings.LastName))
        {
            return ActionResult.Failed(new ArgumentException("Last Name is required."), StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.Company))
        {
            return ActionResult.Failed(new ArgumentException("Company is required."), StepRunErrorCategory.Validation);
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

        // Named fields always win over AdditionalFields — they're the primary, purpose-built
        // inputs; AdditionalFields exists only to extend, not override, them.
        fields["LastName"] = settings.LastName;
        fields["Company"] = settings.Company;

        if (!string.IsNullOrWhiteSpace(settings.FirstName))
        {
            fields["FirstName"] = settings.FirstName;
        }

        if (!string.IsNullOrWhiteSpace(settings.Email))
        {
            fields["Email"] = settings.Email;
        }

        if (!string.IsNullOrWhiteSpace(settings.Phone))
        {
            fields["Phone"] = settings.Phone;
        }

        if (!string.IsNullOrWhiteSpace(settings.Title))
        {
            fields["Title"] = settings.Title;
        }

        if (!string.IsNullOrWhiteSpace(settings.LeadSource))
        {
            fields["LeadSource"] = settings.LeadSource;
        }

        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            fields["Status"] = settings.Status;
        }

        var apiVersion = _options.CurrentValue.ApiVersion;
        var result = await _client.SendAsync(
            connection!,
            HttpMethod.Post,
            $"/services/data/{apiVersion}/sobjects/Lead",
            fields,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return SalesforceActionSupport.Failed(result);
        }

        var recordId = result.Json?.TryGetProperty("id", out var idElement) == true ? idElement.GetString() : null;
        return Success(new CreateLeadOutput { RecordId = recordId });
    }
}
