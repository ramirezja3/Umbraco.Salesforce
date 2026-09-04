using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Community.Automate.Salesforce.Api;
using Umbraco.Community.Automate.Salesforce.Configuration;
using Umbraco.Community.Automate.Salesforce.Connection;

namespace Umbraco.Community.Automate.Salesforce.Actions;

/// <summary>
/// Creates or updates a Salesforce Contact with named fields for the common attributes — the
/// "known customer" counterpart to <see cref="CreateLeadAction"/>. Requires a Salesforce
/// connection with the <c>api</c> scope.
/// </summary>
/// <remarks>
/// Switches between create and update via <see cref="CreateOrUpdateContactSettings.ContactId"/>
/// rather than an implicit lookup (e.g. by email) — Salesforce's standard Contact <c>Email</c>
/// field isn't an External ID, so there's no REST-native "upsert by email" for it, and this
/// package deliberately doesn't run its own SOQL lookup behind the scenes to fake one. The
/// intended flow: create on a customer's first web action and store the returned
/// <see cref="CreateOrUpdateContactOutput.RecordId"/> (e.g. on an Umbraco member), then bind that
/// Id back into <see cref="CreateOrUpdateContactSettings.ContactId"/> on later automations to
/// update the same Contact.
/// </remarks>
[Action("salesforce.createOrUpdateContact", "Create/Update Contact",
    Description = "Creates a new Salesforce Contact, or updates one by Id.",
    Group = "Salesforce",
    Icon = "icon-add",
    ConnectionTypeAlias = "salesforce")]
public sealed class CreateOrUpdateContactAction : ActionBase<CreateOrUpdateContactSettings, CreateOrUpdateContactOutput>
{
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOrUpdateContactAction"/> class.
    /// </summary>
    public CreateOrUpdateContactAction(
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
        var settings = context.GetSettings<CreateOrUpdateContactSettings>();

        if (string.IsNullOrWhiteSpace(settings.LastName))
        {
            return ActionResult.Failed(new ArgumentException("Last Name is required."), StepRunErrorCategory.Validation);
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

        // Named fields always win over AdditionalFields — same rule as CreateLeadAction.
        fields["LastName"] = settings.LastName;

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

        if (!string.IsNullOrWhiteSpace(settings.AccountId))
        {
            fields["AccountId"] = settings.AccountId;
        }

        var apiVersion = _options.CurrentValue.ApiVersion;
        var isUpdate = !string.IsNullOrWhiteSpace(settings.ContactId);
        var path = isUpdate
            ? $"/services/data/{apiVersion}/sobjects/Contact/{Uri.EscapeDataString(settings.ContactId!)}"
            : $"/services/data/{apiVersion}/sobjects/Contact";

        var result = await _client.SendAsync(
            connection!,
            isUpdate ? HttpMethod.Patch : HttpMethod.Post,
            path,
            fields,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return SalesforceActionSupport.Failed(result);
        }

        // A successful PATCH update returns 204 No Content — echo back the Id we were given.
        // A successful POST create returns the new record's Id in the response body.
        var recordId = isUpdate
            ? settings.ContactId
            : result.Json?.TryGetProperty("id", out var idElement) == true ? idElement.GetString() : null;

        return Success(new CreateOrUpdateContactOutput { RecordId = recordId, Created = !isUpdate });
    }
}
