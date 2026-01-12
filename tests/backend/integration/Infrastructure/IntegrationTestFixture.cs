using Microsoft.Extensions.DependencyInjection;
using Waterblocks.Api.Infrastructure.Db;
using Xunit;

namespace Waterblocks.IntegrationTests.Infrastructure;

/// <summary>
/// Base fixture for integration tests.
/// Manages the test database and web application factory lifecycle.
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    private TestDatabase? _database;
    private TestWebApplicationFactory? _factory;

    public AdminApiClient AdminClient { get; private set; } = null!;
    public FireblocksApiClient FireblocksClient { get; private set; } = null!;
    public HttpClient HttpClient { get; private set; } = null!;
    public string WorkspaceId { get; private set; } = string.Empty;
    public string ApiKey { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Create isolated test database
        _database = await TestDatabase.CreateAsync();

        // Create web application factory with test database
        _factory = new TestWebApplicationFactory(_database.ConnectionString);

        // Create HTTP client
        HttpClient = _factory.CreateClient();
        AdminClient = new AdminApiClient(HttpClient);
        FireblocksClient = new FireblocksApiClient(HttpClient);

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
            db.Assets.Add(new Waterblocks.Api.Models.Asset
            {
                AssetId = "BTC",
                Name = "Bitcoin",
                Symbol = "BTC",
                Decimals = 8,
                Type = "BASE_ASSET",
                BlockchainType = Waterblocks.Api.Models.BlockchainType.AddressBased,
                NativeAsset = "BTC",
                BaseFee = 0.0001m,
                FeeAssetId = "BTC",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        // Seed ETH asset if not exists
        if (!db.Assets.Any(a => a.AssetId == "ETH"))
        {
            db.Assets.Add(new Waterblocks.Api.Models.Asset
            {
                AssetId = "ETH",
                Name = "Ethereum",
                Symbol = "ETH",
                Decimals = 18,
                Type = "BASE_ASSET",
                BlockchainType = Waterblocks.Api.Models.BlockchainType.AccountBased,
                NativeAsset = "ETH",
                BaseFee = 0.002m,
                FeeAssetId = "ETH",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();

        // Create default workspace (which also creates an API key)
        var workspaceResponse = await AdminClient.CreateWorkspaceAsync("TestWorkspace");
        if (workspaceResponse.Data != null)
        {
            WorkspaceId = workspaceResponse.Data.Id;
            ApiKey = workspaceResponse.Data.ApiKeys.FirstOrDefault()?.Key ?? string.Empty;
            AdminClient.SetWorkspace(WorkspaceId);
            FireblocksClient.SetApiKey(ApiKey);
        }
    }

    /// <summary>
    /// Creates a new workspace and returns its details including API key.
    /// Useful for multi-workspace tests.
    /// </summary>
    public async Task<(string WorkspaceId, string ApiKey)> CreateWorkspaceAsync(string name)
    {
        var response = await AdminClient.CreateWorkspaceAsync(name);
        if (response.Data == null)
        {
            throw new InvalidOperationException($"Failed to create workspace: {response.Error?.Message}");
        }

        var workspaceId = response.Data.Id;
        var apiKey = response.Data.ApiKeys.FirstOrDefault()?.Key ?? string.Empty;
        return (workspaceId, apiKey);
    }

    /// <summary>
    /// Creates a new HttpClient with a specific API key for Fireblocks API testing.
    /// </summary>
    public FireblocksApiClient CreateFireblocksClientWithApiKey(string apiKey)
    {
        var client = new FireblocksApiClient(_factory!.CreateClient());
        client.SetApiKey(apiKey);
        return client;
    }

    /// <summary>
    /// Creates a new AdminApiClient for a specific workspace.
    /// </summary>
    public AdminApiClient CreateAdminClientForWorkspace(string workspaceId)
    {
        var client = new AdminApiClient(_factory!.CreateClient());
        client.SetWorkspace(workspaceId);
        return client;
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

    public T GetRequiredService<T>() where T : notnull
    {
        return _factory!.Services.GetRequiredService<T>();
    }
}
