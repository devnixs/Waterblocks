using Microsoft.Extensions.DependencyInjection;
using FireblocksReplacement.Api.Infrastructure.Db;
using Xunit;

namespace FireblocksReplacement.IntegrationTests.Infrastructure;

/// <summary>
/// Base fixture for integration tests.
/// Manages the test database and web application factory lifecycle.
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    private TestDatabase? _database;
    private TestWebApplicationFactory? _factory;

    public AdminApiClient AdminClient { get; private set; } = null!;
    public HttpClient HttpClient { get; private set; } = null!;
    public string WorkspaceId { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Create isolated test database
        _database = await TestDatabase.CreateAsync();

        // Create web application factory with test database
        _factory = new TestWebApplicationFactory(_database.ConnectionString);

        // Create HTTP client
        HttpClient = _factory.CreateClient();
        AdminClient = new AdminApiClient(HttpClient);

        // Seed required data (assets) and create a default workspace
        await SeedRequiredDataAsync();
    }

    private async Task SeedRequiredDataAsync()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FireblocksDbContext>();

        // Seed BTC asset if not exists
        if (!db.Assets.Any(a => a.AssetId == "BTC"))
        {
            db.Assets.Add(new FireblocksReplacement.Api.Models.Asset
            {
                AssetId = "BTC",
                Name = "Bitcoin",
                Symbol = "BTC",
                Decimals = 8,
                Type = "BASE_ASSET",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        // Seed ETH asset if not exists
        if (!db.Assets.Any(a => a.AssetId == "ETH"))
        {
            db.Assets.Add(new FireblocksReplacement.Api.Models.Asset
            {
                AssetId = "ETH",
                Name = "Ethereum",
                Symbol = "ETH",
                Decimals = 18,
                Type = "BASE_ASSET",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();

        // Create default workspace
        var workspaceResponse = await AdminClient.CreateWorkspaceAsync("TestWorkspace");
        if (workspaceResponse.Data != null)
        {
            WorkspaceId = workspaceResponse.Data.Id;
            AdminClient.SetWorkspace(WorkspaceId);
        }
    }

    public async Task DisposeAsync()
    {
        HttpClient?.Dispose();
        _factory?.Dispose();

        if (_database != null)
        {
            await _database.DisposeAsync();
        }
    }

    /// <summary>
    /// Gets the database context for direct database operations in tests.
    /// </summary>
    public FireblocksDbContext GetDbContext()
    {
        var scope = _factory!.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<FireblocksDbContext>();
    }
}
