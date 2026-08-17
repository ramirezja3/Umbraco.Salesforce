using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Salesforce.Persistence;

namespace Umbraco.Automate.Salesforce.Tests.Integration;

/// <summary>
/// Verifies the generated SQLite migration applies cleanly to a fresh database and is
/// idempotent (running it twice is a no-op) — CLAUDE.md §9. No live Salesforce org needed;
/// this only exercises the polling-state table's own schema.
/// </summary>
public class SalesforceDbContextMigrationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"salesforce-migration-test-{Guid.NewGuid():N}.sqlite");

    [Fact]
    public async Task Migrate_OnFreshDatabase_CreatesPollingStateTable()
    {
        await using var db = CreateContext();

        await db.Database.MigrateAsync();

        var tableExists = await TableExistsAsync(db, "umbracoAutomateSalesforcePollingState");
        tableExists.ShouldBeTrue();
    }

    [Fact]
    public async Task Migrate_RunTwice_IsANoOp()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        // Second migrate against the same database must not throw and must leave no pending migrations.
        await Should.NotThrowAsync(async () =>
        {
            await using var db2 = CreateContext();
            await db2.Database.MigrateAsync();
        });

        await using var db3 = CreateContext();
        var pending = await db3.Database.GetPendingMigrationsAsync();
        pending.ShouldBeEmpty();
    }

    [Fact]
    public async Task PollingStateStore_RoundTrips_ThroughTheMigratedSchema()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var automationId = Guid.NewGuid();
        db.PollingStates.Add(new SalesforcePollingStateEntity
        {
            AutomationId = automationId,
            LastPollUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SnapshotJson = """{"006abc":"Closed Won"}""",
        });
        await db.SaveChangesAsync();

        await using var readDb = CreateContext();
        var loaded = await readDb.PollingStates.SingleAsync(e => e.AutomationId == automationId);
        loaded.LastPollUtc.ShouldBe(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        loaded.SnapshotJson.ShouldNotBeNull().ShouldContain("Closed Won");
    }

    private SalesforceDbContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SalesforceDbContext>();
        optionsBuilder.UseSqlite(
            $"Data Source={_dbPath}",
            x => x.MigrationsAssembly("Umbraco.Automate.Salesforce.Persistence.Sqlite"));
        return new SalesforceDbContext(optionsBuilder.Options);
    }

    private static async Task<bool> TableExistsAsync(SalesforceDbContext db, string tableName)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync();
        return result is not null;
    }

    public void Dispose()
    {
        // SQLite pools connections by connection string, which keeps the file locked past the
        // owning DbContext's disposal — clear the pool first or the delete races it.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
