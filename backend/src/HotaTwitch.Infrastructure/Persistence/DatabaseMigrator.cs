using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HotaTwitch.Infrastructure.Persistence;

/// <summary>Applies pending migrations before the service starts serving.</summary>
internal sealed class DatabaseMigrator(IServiceScopeFactory scopeFactory, ILogger<DatabaseMigrator> logger)
    : IHostedService
{
    private const int Attempts = 5;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<HotaTwitchDbContext>();

            try
            {
                await database.Database.MigrateAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is DbUpdateException or Microsoft.Data.Sqlite.SqliteException && attempt < Attempts)
            {
                DatabaseMigratorLog.AttemptFailed(logger, attempt, Attempts, exception);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static partial class DatabaseMigratorLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Database migration attempt {Attempt} of {Attempts} failed; retrying.")]
    public static partial void AttemptFailed(ILogger logger, int attempt, int attempts, Exception exception);
}
