using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure.Db;

namespace Waterblocks.Api.Infrastructure;

public static class SeedData
{
    private static readonly HashSet<string> EvmNativeAssets = new(StringComparer.OrdinalIgnoreCase)
    {
        "ETH",
        "MATIC_POLYGON",
        "BNB_BSC",
        "AVAX_C",
        "BASECHAIN_ETH",
    };

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
        var assetsPath = Path.Combine(AppContext.BaseDirectory, "all_assets.json");
        if (!File.Exists(assetsPath))
        {
            logger.LogWarning("Asset seed file not found at {Path}", assetsPath);
            return;
        }

        var json = File.ReadAllText(assetsPath);
        var allAssets = JsonSerializer.Deserialize<List<AssetSeed>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? new List<AssetSeed>();

        var existingSymbols = db.Assets
            .Select(a => a.Symbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingAssetIds = db.Assets
            .Select(a => a.AssetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in allAssets)
        {
            var symbol = seed.Symbol ?? seed.ApiId ?? seed.Code;
            if (string.IsNullOrWhiteSpace(symbol) || symbol.Equals("N/A", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Skipping asset seed with missing symbol (name: {Name})", seed.Description ?? seed.Name ?? seed.Code ?? "unknown");
                continue;
            }

            if (existingSymbols.Contains(symbol))
            {
                continue;
            }

            if (!Enum.TryParse<Models.BlockchainType>(seed.BlockchainType ?? string.Empty, true, out var blockchainType))
            {
                blockchainType = Models.BlockchainType.AccountBased;
            }

            var assetId = symbol.Trim();
            if (existingAssetIds.Contains(assetId))
            {
                logger.LogWarning(
                    "Skipping asset seed with duplicate AssetId {AssetId} for symbol {Symbol}",
                    assetId, symbol);
                continue;
            }

            var asset = new Waterblocks.Api.Models.Asset
            {
                AssetId = assetId,
                Name = seed.Description ?? seed.Name ?? seed.Code ?? assetId,
                Symbol = symbol,
                Decimals = seed.Decimals ?? 0,
                Type = seed.Type,
                BlockchainType = blockchainType,
                ContractAddress = string.IsNullOrWhiteSpace(seed.ContractAddress) ? null : seed.ContractAddress,
                NativeAsset = string.IsNullOrWhiteSpace(seed.NativeAsset) ? null : seed.NativeAsset,
                BaseFee = seed.BaseFee ?? 0,
                FeeAssetId = string.IsNullOrWhiteSpace(seed.FuelAssetId) ? null : seed.FuelAssetId,
                IsCaseSensitive = seed.IsCaseSensitive ?? InferCaseSensitivity(seed),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            db.Assets.Add(asset);
            existingSymbols.Add(symbol);
            existingAssetIds.Add(assetId);
        }

        db.SaveChanges();
    }

    private static bool InferCaseSensitivity(AssetSeed seed)
    {
        if (!string.IsNullOrWhiteSpace(seed.ContractAddress) &&
            seed.ContractAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(seed.NativeAsset) && EvmNativeAssets.Contains(seed.NativeAsset))
        {
            return false;
        }

        return true;
    }
}
