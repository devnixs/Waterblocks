using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure.Db;

namespace Waterblocks.Api.Infrastructure;

public static class SeedData
{
    public static void SeedDatabase(IServiceProvider services, Microsoft.Extensions.Logging.ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FireblocksDbContext>();
        db.Database.Migrate();
        SeedHelpers.SeedWorkspaces(db, logger);
        SeedAssets(db, logger);
    }

    private static void SeedAssets(FireblocksDbContext db, Microsoft.Extensions.Logging.ILogger logger)
    {
        var assetsPath = Path.Combine(AppContext.BaseDirectory, "all_fireblocks_assets.json");
        if (!File.Exists(assetsPath))
        {
            logger.LogWarning("Asset seed file not found at {Path}", assetsPath);
            return;
        }

        var json = File.ReadAllText(assetsPath);
        var allAssets = JsonSerializer.Deserialize<List<FireblocksAssetSeed>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? new List<FireblocksAssetSeed>();

        var requiredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BTC",
            "ETH",
            "ADA",
            "AAVE",
            "AXS",
            "BNB_BSC",
            "BNB_ERC20",
            "BONK_SOL",
            "EUROC_ETH_F5NG",
            "EURR_ETH_YSHA",
            "MATIC",
            "MATIC_POLYGON",
            "SOL",
            "SUSD",
            "USDC",
            "USDC_POLYGON",
            "USDC_POLYGON_NXTB",
            "USDS",
            "USDT_ERC20",
        };

        var byId = allAssets.ToDictionary(a => a.Id, a => a, StringComparer.OrdinalIgnoreCase);
        foreach (var id in requiredIds)
        {
            if (!byId.TryGetValue(id, out var seed))
            {
                logger.LogWarning("Seed asset not found in JSON: {AssetId}", id);
                continue;
            }

            var asset = db.Assets.FirstOrDefault(a => a.AssetId == seed.Id);
            if (asset == null)
            {
                asset = new Waterblocks.Api.Models.Asset
                {
                    AssetId = seed.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                db.Assets.Add(asset);
            }

            asset.Name = seed.Name ?? seed.Id;
            asset.Type = seed.Type;
            asset.ContractAddress = string.IsNullOrWhiteSpace(seed.ContractAddress) ? null : seed.ContractAddress;
            asset.NativeAsset = seed.NativeAsset;
            asset.Decimals = seed.Decimals ?? 0;
            asset.Symbol = SeedHelpers.DeriveSymbol(seed.Id);
            asset.IsActive = true;
        }

        db.SaveChanges();
    }
}
