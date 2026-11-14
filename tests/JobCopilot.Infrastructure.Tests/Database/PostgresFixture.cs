using JobCopilot.Application.Abstractions;
using JobCopilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace JobCopilot.Infrastructure.Tests.Database;

/// <summary>
/// Provides a real PostgreSQL server for the tests in the "Database" collection.
/// <list type="bullet">
///   <item>Default: starts a throw-away container with Testcontainers (needs Docker).</item>
///   <item><c>JOBCOPILOT_TEST_POSTGRES</c>: use an existing server instead (a superuser connection string).</item>
///   <item>Neither available: the tests are skipped – unless <c>JOBCOPILOT_REQUIRE_DB_TESTS=true</c>,
///   which CI sets so that a broken Docker setup fails the build instead of hiding the tests.</item>
/// </list>
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ConnectionStringVariable = "JOBCOPILOT_TEST_POSTGRES";
    public const string RequireVariable = "JOBCOPILOT_REQUIRE_DB_TESTS";

    private PostgreSqlContainer? _container;
    private string? _serverConnectionString;

    /// <summary>Null when a server is available; otherwise why the tests cannot run.</summary>
    public string? UnavailableReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (!string.IsNullOrWhiteSpace(external))
        {
            _serverConnectionString = external;
            return;
        }

        try
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await _container.StartAsync();
            _serverConnectionString = _container.GetConnectionString();
        }
        catch (Exception ex) when (!IsRequired)
        {
            UnavailableReason =
                $"No PostgreSQL available: Docker could not start a container ({ex.GetType().Name}) " +
                $"and {ConnectionStringVariable} is not set.";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>Creates an empty, fully migrated database that is dropped when disposed.</summary>
    public async Task<TestDatabase> CreateDatabaseAsync()
    {
        Assert.SkipWhen(UnavailableReason is not null, UnavailableReason ?? "");

        var name = "jobcopilot_test_" + Guid.NewGuid().ToString("N");
        await ExecuteOnServerAsync($"CREATE DATABASE \"{name}\"");

        var connectionString = new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = name }.ConnectionString;
        var database = new TestDatabase(connectionString, () => DropAsync(name, connectionString));

        await using var db = database.CreateContext();
        await db.Database.MigrateAsync();
        return database;
    }

    private static bool IsRequired =>
        string.Equals(Environment.GetEnvironmentVariable(RequireVariable), "true", StringComparison.OrdinalIgnoreCase);

    private async Task DropAsync(string name, string connectionString)
    {
        // Pooled connections would keep the database busy.
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            NpgsqlConnection.ClearPool(connection);
        }

        await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
    }

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Database";
}

/// <summary>One isolated database for one test.</summary>
public sealed class TestDatabase(string connectionString, Func<Task> drop) : IAsyncDisposable
{
    private readonly DbContextOptions<AppDbContext> _options =
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;

    public string ConnectionString { get; } = connectionString;

    /// <summary>The same factory abstraction the application services use in production.</summary>
    public IAppDbContextFactory Factory => new AppDbContextFactory(new EfFactory(_options));

    public AppDbContext CreateContext() => new(_options);

    public async ValueTask DisposeAsync() => await drop();

    private sealed class EfFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
