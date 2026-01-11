using Microsoft.EntityFrameworkCore;
using Npgsql;
using FireblocksReplacement.Api.Infrastructure.Db;

namespace FireblocksReplacement.IntegrationTests.Infrastructure;

/// <summary>
/// Manages a test database with a random name for isolation.
/// Creates the database on initialization and drops it on disposal.
/// </summary>
public class TestDatabase : IAsyncDisposable
{
    private const string PostgresHost = "localhost";
    private const int PostgresPort = 5432;
    private const string PostgresUser = "postgres";
    private const string PostgresPassword = "postgres";

    public string DatabaseName { get; }
    public string ConnectionString { get; }

    private TestDatabase(string databaseName)
    {
        DatabaseName = databaseName;
        ConnectionString = BuildConnectionString(databaseName);
    }

    public static async Task<TestDatabase> CreateAsync()
    {
        var databaseName = $"test_{Guid.NewGuid():N}";
        var testDb = new TestDatabase(databaseName);
        await testDb.InitializeAsync();
        return testDb;
    }

    private async Task InitializeAsync()
    {
        // Connect to postgres database to create the test database
        var masterConnectionString = BuildConnectionString("postgres");

        await using var connection = new NpgsqlConnection(masterConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{DatabaseName}\"";
        await cmd.ExecuteNonQueryAsync();

        // Apply migrations
        var options = new DbContextOptionsBuilder<FireblocksDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var context = new FireblocksDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Connect to postgres database to drop the test database
        var masterConnectionString = BuildConnectionString("postgres");

        await using var connection = new NpgsqlConnection(masterConnectionString);
        await connection.OpenAsync();

        // Terminate existing connections to the test database
        await using var terminateCmd = connection.CreateCommand();
        terminateCmd.CommandText = $@"
            SELECT pg_terminate_backend(pg_stat_activity.pid)
            FROM pg_stat_activity
            WHERE pg_stat_activity.datname = '{DatabaseName}'
            AND pid <> pg_backend_pid()";
        await terminateCmd.ExecuteNonQueryAsync();

        // Drop the database
        await using var dropCmd = connection.CreateCommand();
        dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{DatabaseName}\"";
        await dropCmd.ExecuteNonQueryAsync();
    }

    private static string BuildConnectionString(string database)
    {
        return $"Host={PostgresHost};Port={PostgresPort};Database={database};Username={PostgresUser};Password={PostgresPassword}";
    }
}
