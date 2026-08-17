using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Salesforce.Persistence;

/// <summary>
/// Notification handler that runs pending EF Core migrations for the Salesforce polling-state
/// table on startup. Mirrors <c>Umbraco.Automate.OpenIddict.Core</c>'s
/// <c>RunOpenIddictMigrationNotificationHandler</c> exactly, including the same
/// standalone-DbContext workaround for the pooled-connection/MiniProfiler issue it documents.
/// </summary>
internal sealed class RunSalesforceMigrationNotificationHandler
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<ConnectionStrings> _connectionStrings;

    public RunSalesforceMigrationNotificationHandler(
        IConfiguration configuration,
        IOptionsMonitor<ConnectionStrings> connectionStrings)
    {
        _configuration = configuration;
        _connectionStrings = connectionStrings;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        var (connectionString, providerName) = DatabaseConnectionInfo.Resolve(_connectionStrings, _configuration);
        var optionsBuilder = new DbContextOptionsBuilder<SalesforceDbContext>();
        SalesforceDbContext.ConfigureProvider(optionsBuilder, connectionString, providerName);

        await using SalesforceDbContext dbContext = new SalesforceDbContext(optionsBuilder.Options);

        IEnumerable<string> pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }
}
