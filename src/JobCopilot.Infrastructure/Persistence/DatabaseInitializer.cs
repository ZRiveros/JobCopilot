using System.Net.Sockets;
using JobCopilot.Application.JobAds;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace JobCopilot.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations at start-up and seeds first-run data. Waits for the database for a
/// while, because the web container can come up before PostgreSQL accepts connections.
/// </summary>
public sealed class DatabaseInitializer(
    IDbContextFactory<AppDbContext> dbFactory,
    SavedSearchService savedSearches,
    ILogger<DatabaseInitializer> logger)
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await MigrateAsync(cancellationToken);
        await savedSearches.SeedDefaultsAsync(cancellationToken);
    }

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database is up to date");
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsConnectionFailure(ex))
            {
                logger.LogWarning(
                    "Database not reachable yet (attempt {Attempt}/{MaxAttempts}): {Message}",
                    attempt, MaxAttempts, ex.Message);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Only connectivity problems are worth waiting for. A wrong password or a broken migration
    /// will not fix themselves, so errors reported by the server fail fast unless PostgreSQL
    /// itself flags them as transient ("the database system is starting up").
    /// </summary>
    private static bool IsConnectionFailure(Exception exception) => exception switch
    {
        PostgresException serverError => serverError.IsTransient,
        NpgsqlException or SocketException or TimeoutException => true,
        _ => exception.InnerException is { } inner && IsConnectionFailure(inner),
    };
}
