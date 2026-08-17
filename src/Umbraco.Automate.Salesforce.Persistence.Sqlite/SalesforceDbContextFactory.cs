using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.Salesforce.Persistence;

namespace Umbraco.Automate.Salesforce.Persistence.Sqlite;

/// <summary>
/// Design-time factory for creating <see cref="SalesforceDbContext"/> with SQLite.
/// </summary>
public class SalesforceDbContextFactory : IDesignTimeDbContextFactory<SalesforceDbContext>
{
    /// <inheritdoc />
    public SalesforceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SalesforceDbContext>();
        optionsBuilder.UseSqlite(
            "Data Source=UmbracoAutomate.sqlite.db",
            x =>
            {
                x.MigrationsAssembly("Umbraco.Automate.Salesforce.Persistence.Sqlite");
                x.MigrationsHistoryTable(DatabaseConnectionInfo.MigrationsHistoryTable);
            });
        return new SalesforceDbContext(optionsBuilder.Options);
    }
}
