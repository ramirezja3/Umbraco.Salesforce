using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Cms.Core;

namespace Umbraco.Automate.Salesforce.Persistence;

/// <summary>
/// EF Core database context for Umbraco Automate Salesforce entities. Owns the polling-state
/// checkpoint table only (§0a) — everything else this package needs is either read from
/// Core's own <c>Connections</c> table or reuses <c>OAuthCredentials.AccountLabel</c>.
/// </summary>
/// <remarks>
/// Unlike <c>Umbraco.Automate.OpenIddict.Core</c>'s <c>OpenIddictDbContext</c>, this can't call
/// Core's internal <c>AutomateDbProvider</c>/<c>AutomateMigrationsAssemblies</c> helpers — Core's
/// <c>InternalsVisibleTo</c> list is a fixed allowlist that doesn't include this assembly (verified
/// by reading <c>Umbraco.Automate.Core.csproj</c>), so the SqlServer/Sqlite provider switch is
/// inlined here instead. <c>DatabaseConnectionInfo</c> is public and safe to reuse as-is.
/// </remarks>
public sealed class SalesforceDbContext : DbContext
{
    internal DbSet<SalesforcePollingStateEntity> PollingStates { get; set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="SalesforceDbContext"/> class.
    /// </summary>
    public SalesforceDbContext(DbContextOptions<SalesforceDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Configures the EF Core database provider with the correct migrations assembly.
    /// </summary>
    internal static void ConfigureProvider(
        DbContextOptionsBuilder options,
        string connectionString,
        string providerName)
    {
        switch (providerName)
        {
            case Constants.ProviderNames.SQLServer:
                options.UseSqlServer(connectionString, x =>
                {
                    x.MigrationsAssembly("Umbraco.Automate.Salesforce.Persistence.SqlServer");
                    x.MigrationsHistoryTable(DatabaseConnectionInfo.MigrationsHistoryTable);
                    x.EnableRetryOnFailure();
                });
                break;

            case Constants.ProviderNames.SQLLite:
            case "Microsoft.Data.SQLite":
                options.UseSqlite(connectionString, x =>
                {
                    x.MigrationsAssembly("Umbraco.Automate.Salesforce.Persistence.Sqlite");
                    x.MigrationsHistoryTable(DatabaseConnectionInfo.MigrationsHistoryTable);
                });
                break;

            default:
                throw new InvalidOperationException(
                    $"Database provider '{providerName}' is not supported. Supported: SQL Server, SQLite.");
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SalesforcePollingStateEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateSalesforcePollingState");
            entity.HasKey(e => e.AutomationId);

            entity.Property(e => e.SnapshotJson);
            entity.Property(e => e.DateModified).IsRequired();
        });
    }
}
