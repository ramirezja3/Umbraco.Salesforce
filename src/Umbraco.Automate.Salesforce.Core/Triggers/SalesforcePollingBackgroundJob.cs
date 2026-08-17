using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Salesforce.Api;
using Umbraco.Automate.Salesforce.Connection;
using Umbraco.Automate.Salesforce.Persistence;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.HostedServices;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Background service that polls published automations with Salesforce polling triggers and
/// dispatches matching events. Mirrors Core's own <c>ScheduledTriggerBackgroundJob</c> — same
/// MainDom/server-role guards, same per-automation try/catch-and-continue shape — swapping the
/// CRON-due check for a call into <see cref="ISalesforcePollingTrigger.PollAsync"/>.
/// </summary>
internal sealed class SalesforcePollingBackgroundJob : RecurringHostedServiceBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRuntimeState _runtimeState;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IMainDom _mainDom;
    private readonly ILogger<SalesforcePollingBackgroundJob> _logger;

    public SalesforcePollingBackgroundJob(
        IServiceProvider serviceProvider,
        IOptionsMonitor<SalesforcePollingOptions> options,
        IRuntimeState runtimeState,
        IServerRoleAccessor serverRoleAccessor,
        IMainDom mainDom,
        ILogger<SalesforcePollingBackgroundJob> logger)
        : base(logger, options.CurrentValue.PollInterval, options.CurrentValue.StartupDelay)
    {
        _serviceProvider = serviceProvider;
        _runtimeState = runtimeState;
        _serverRoleAccessor = serverRoleAccessor;
        _mainDom = mainDom;
        _logger = logger;
    }

    public override async Task PerformExecuteAsync(object? state)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        switch (_serverRoleAccessor.CurrentServerRole)
        {
            case ServerRole.Subscriber:
                _logger.LogDebug("Salesforce polling job will not run on subscriber servers");
                return;
            case ServerRole.Unknown:
                _logger.LogDebug("Salesforce polling job will not run on servers with unknown role");
                return;
            case ServerRole.Single:
            case ServerRole.SchedulingPublisher:
            default:
                break;
        }

        if (!_mainDom.IsMainDom)
        {
            _logger.LogDebug("Salesforce polling job will not run if not MainDom");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
        var triggerCollection = scope.ServiceProvider.GetRequiredService<TriggerCollection>();
        var connectionResolver = scope.ServiceProvider.GetRequiredService<ISalesforceTriggerConnectionResolver>();
        var salesforceConnectionResolver = scope.ServiceProvider.GetRequiredService<ISalesforceConnectionResolver>();
        var stateStore = scope.ServiceProvider.GetRequiredService<ISalesforcePollingStateStore>();
        var client = scope.ServiceProvider.GetRequiredService<ISalesforceClient>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ITriggerDispatcher>();

        var publishedRefs = await automationService.GetPublishedVersionReferencesAsync(CancellationToken.None);
        var pollStartedUtc = DateTime.UtcNow;

        foreach (var (automationId, _) in publishedRefs)
        {
            try
            {
                var automation = await automationService.GetAutomationAsync(automationId, CancellationToken.None);
                if (automation?.Trigger is null)
                {
                    continue;
                }

                var trigger = triggerCollection.GetByAlias(automation.Trigger.TriggerAlias);
                if (trigger is not ISalesforcePollingTrigger pollingTrigger)
                {
                    continue;
                }

                var connection = await connectionResolver.ResolveAsync(automationId, CancellationToken.None);
                if (connection is null)
                {
                    // Already logged (with reason) by the resolver.
                    continue;
                }

                if (connection.Settings is not ISalesforceConnectionSettings connectionSettings
                    || connectionSettings.OAuthCredentialsId is not { } credentialsId)
                {
                    continue;
                }

                var salesforceConnection = await salesforceConnectionResolver.ResolveAsync(credentialsId, CancellationToken.None);
                if (salesforceConnection is null)
                {
                    _logger.LogWarning(
                        "Salesforce connection '{ConnectionId}' for automation {AutomationId} is not authenticated (or its token can't be refreshed) — skipping this poll",
                        connection.Id, automationId);
                    continue;
                }

                object? triggerSettings = null;
                if (trigger.SettingsType is not null && automation.Trigger.Settings.Count > 0)
                {
                    triggerSettings = trigger.ResolveSettings(automation.Trigger.Settings);
                }

                var previousState = await stateStore.GetStateAsync(automationId, CancellationToken.None);

                var pollingContext = new SalesforcePollingContext
                {
                    AutomationId = automationId,
                    TriggerAlias = automation.Trigger.TriggerAlias,
                    Settings = triggerSettings,
                    Connection = salesforceConnection,
                    Client = client,
                    PreviousState = previousState,
                    PollStartedUtc = pollStartedUtc,
                };

                var result = await pollingTrigger.PollAsync(pollingContext, CancellationToken.None);

                foreach (var triggerEvent in result.Events)
                {
                    await dispatcher.DispatchAsync(triggerEvent, CancellationToken.None);
                }

                await stateStore.SaveStateAsync(automationId, result.NextState, CancellationToken.None);

                if (result.Events.Count > 0)
                {
                    _logger.LogInformation(
                        "Salesforce polling trigger for automation {AutomationId} dispatched {EventCount} event(s)",
                        automationId, result.Events.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to poll Salesforce trigger for automation {AutomationId}", automationId);
            }
        }
    }
}
