using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Umbraco.Automate.Salesforce.Persistence;

/// <inheritdoc cref="ISalesforcePollingStateStore"/>
internal sealed class SalesforcePollingStateStore : ISalesforcePollingStateStore
{
    private readonly IDbContextFactory<SalesforceDbContext> _dbContextFactory;

    public SalesforcePollingStateStore(IDbContextFactory<SalesforceDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<SalesforcePollingState> GetStateAsync(Guid automationId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.PollingStates
            .FirstOrDefaultAsync(e => e.AutomationId == automationId, cancellationToken);

        if (entity is null)
        {
            return SalesforcePollingState.Initial;
        }

        var snapshot = string.IsNullOrEmpty(entity.SnapshotJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.SnapshotJson) ?? [];

        return new SalesforcePollingState(entity.LastPollUtc, snapshot);
    }

    public async Task SaveStateAsync(Guid automationId, SalesforcePollingState state, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.PollingStates
            .FirstOrDefaultAsync(e => e.AutomationId == automationId, cancellationToken);

        var snapshotJson = JsonSerializer.Serialize(state.Snapshot);

        if (entity is null)
        {
            db.PollingStates.Add(new SalesforcePollingStateEntity
            {
                AutomationId = automationId,
                LastPollUtc = state.LastPollUtc,
                SnapshotJson = snapshotJson,
            });
        }
        else
        {
            entity.LastPollUtc = state.LastPollUtc;
            entity.SnapshotJson = snapshotJson;
            entity.DateModified = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
