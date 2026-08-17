using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.Salesforce.Persistence;

namespace Umbraco.Automate.Salesforce.Persistence.SqlServer;

/// <summary>
/// Design-time factory for creating <see cref="SalesforceDbContext"/> with SQL Server.
/// </summary>
public class SalesforceDbContextFactory : IDesignTimeDbContextFactory<SalesforceDbContext>
{
    /// <inheritdoc />
    public SalesforceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SalesforceDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=.;Database=UmbracoAutomate;Trusted_Connection=True;",
            x =>
            {
                x.MigrationsAssembly("Umbraco.Automate.Salesforce.Persistence.SqlServer");
                x.MigrationsHistoryTable(DatabaseConnectionInfo.MigrationsHistoryTable);
            });
        return new SalesforceDbContext(optionsBuilder.Options);
    }
}
