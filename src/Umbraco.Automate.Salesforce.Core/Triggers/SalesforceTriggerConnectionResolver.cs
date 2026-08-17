using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Workspaces;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <inheritdoc cref="ISalesforceTriggerConnectionResolver"/>
internal sealed class SalesforceTriggerConnectionResolver : ISalesforceTriggerConnectionResolver
{
    private static readonly string[] SalesforceConnectionTypeAliases = ["salesforce", "salesforce-sandbox"];

    private readonly IAutomationService _automationService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IConnectionService _connectionService;
    private readonly ILogger<SalesforceTriggerConnectionResolver> _logger;

    public SalesforceTriggerConnectionResolver(
        IAutomationService automationService,
        IWorkspaceService workspaceService,
        IConnectionService connectionService,
        ILogger<SalesforceTriggerConnectionResolver> logger)
    {
        _automationService = automationService;
        _workspaceService = workspaceService;
        _connectionService = connectionService;
        _logger = logger;
    }

    public async Task<ConfiguredConnection?> ResolveAsync(Guid automationId, CancellationToken cancellationToken)
    {
        var automation = await _automationService.GetAutomationAsync(automationId, cancellationToken);
        if (automation is null)
        {
            return null;
        }

        var workspace = await _workspaceService.GetWorkspaceAsync(automation.WorkspaceId, cancellationToken);
        if (workspace is null || workspace.AllowedConnections.Count == 0)
        {
            return null;
        }

        var configured = await _connectionService.GetConfiguredConnectionsByIdsAsync(
            workspace.AllowedConnections.ToList(), cancellationToken);

        var matches = configured
            .Where(c => SalesforceConnectionTypeAliases.Contains(c.Type, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            _logger.LogWarning(
                "No Salesforce connection found in workspace '{WorkspaceId}' for automation {AutomationId}",
                automation.WorkspaceId, automationId);
            return null;
        }

        if (matches.Count > 1)
        {
            _logger.LogWarning(
                "Multiple Salesforce connections found in workspace '{WorkspaceId}' for automation {AutomationId}. Using first match '{ConnectionId}'",
                automation.WorkspaceId, automationId, matches[0].Id);
        }

        return matches[0];
    }
}
