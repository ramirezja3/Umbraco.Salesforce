using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Community.Automate.Salesforce.Api;
using Umbraco.Community.Automate.Salesforce.Configuration;
using Umbraco.Community.Automate.Salesforce.Connection;

namespace Umbraco.Community.Automate.Salesforce.Actions;

/// <summary>
/// Logs a completed activity (a Salesforce Task) against a Contact or Lead — turns an on-site
/// behavioral signal (e.g. an Engage segment match, a re-engagement event) into something a
/// salesperson sees directly on that person's Activity History timeline in Salesforce. Requires
/// a Salesforce connection with the <c>api</c> scope.
/// </summary>
[Action("salesforce.logEngagementActivity", "Log Engagement Activity",
    Description = "Logs a completed activity against a Contact or Lead, visible on their Salesforce timeline.",
    Group = "Salesforce",
    Icon = "icon-add",
    ConnectionTypeAlias = "salesforce")]
public sealed class LogEngagementActivityAction : ActionBase<LogEngagementActivitySettings, LogEngagementActivityOutput>
{
    private readonly ISalesforceConnectionResolver _connectionResolver;
    private readonly ISalesforceClient _client;
    private readonly IOptionsMonitor<SalesforceApiOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogEngagementActivityAction"/> class.
    /// </summary>
    public LogEngagementActivityAction(
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
        var settings = context.GetSettings<LogEngagementActivitySettings>();

        if (string.IsNullOrWhiteSpace(settings.WhoId))
        {
            return ActionResult.Failed(new ArgumentException("Contact or Lead Id is required."), StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.Subject))
        {
            return ActionResult.Failed(new ArgumentException("Subject is required."), StepRunErrorCategory.Validation);
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

        // Status is always "Completed" — this action logs something that already happened, not
        // an assigned to-do, so there's no picklist for the automation author to guess at.
        var fields = new Dictionary<string, object?>
        {
            ["WhoId"] = settings.WhoId,
            ["Subject"] = settings.Subject,
            ["Status"] = "Completed",
        };

        if (!string.IsNullOrWhiteSpace(settings.Description))
        {
            fields["Description"] = settings.Description;
        }

        if (!string.IsNullOrWhiteSpace(settings.ActivityDate))
        {
            fields["ActivityDate"] = settings.ActivityDate;
        }

        var apiVersion = _options.CurrentValue.ApiVersion;
        var result = await _client.SendAsync(
            connection!,
            HttpMethod.Post,
            $"/services/data/{apiVersion}/sobjects/Task",
            fields,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return SalesforceActionSupport.Failed(result);
        }

        var recordId = result.Json?.TryGetProperty("id", out var idElement) == true ? idElement.GetString() : null;
        return Success(new LogEngagementActivityOutput { RecordId = recordId });
    }
}
