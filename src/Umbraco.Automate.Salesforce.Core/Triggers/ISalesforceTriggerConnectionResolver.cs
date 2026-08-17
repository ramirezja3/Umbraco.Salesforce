using Umbraco.Automate.Core.Connections;

namespace Umbraco.Automate.Salesforce.Triggers;

/// <summary>
/// Resolves which Salesforce connection a polling trigger should use.
/// </summary>
/// <remarks>
/// Triggers have no Connection concept in this platform version — confirmed by reading
/// <c>TriggerConfiguration</c> (alias + settings dictionary only, no <c>ConnectionId</c>) and the
/// trigger-settings frontend modal (no connection-picker code at all, unlike the per-step one
/// actions use). See CLAUDE.md §0a. Rather than add a duplicate "authenticate again" OAuth field
/// to every trigger's settings, this mirrors Core's own <c>ActionStepBody.ResolveConnectionByTypeAsync</c>
/// fallback: find a connection of the right type among the automation's workspace's
/// <c>AllowedConnections</c>, logging a warning (not failing) if more than one exists.
/// </remarks>
public interface ISalesforceTriggerConnectionResolver
{
    /// <summary>
    /// Resolves a configured Salesforce connection (production or sandbox — whichever the
    /// workspace has) for the given automation, or <c>null</c> if the workspace has none.
    /// </summary>
    Task<ConfiguredConnection?> ResolveAsync(Guid automationId, CancellationToken cancellationToken);
}
