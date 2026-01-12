using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FireblocksReplacement.Api.Infrastructure.Db;
using FireblocksReplacement.Api.Dtos.Fireblocks;

namespace FireblocksReplacement.Api.Controllers;

[ApiController]
public class AssetsController : ControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<AssetsController> _logger;

    public AssetsController(FireblocksDbContext context, ILogger<AssetsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("supported_assets")]
    public async Task<ActionResult<List<AssetDto>>> GetSupportedAssets()
    {
        var assets = await _context.Assets
            .Where(a => a.IsActive)
            .ToListAsync();

        var dtos = assets.Select(a => new AssetDto
        {
            Id = a.AssetId,
            Name = a.Name,
            Symbol = a.Symbol,
            Decimals = a.Decimals,
            Type = a.Type,
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("vault/assets")]
    public async Task<ActionResult<List<VaultAssetDto>>> GetVaultAssets()
    {
        var wallets = await _context.Wallets
            .GroupBy(w => w.AssetId)
            .Select(g => new VaultAssetDto
            {
                Id = g.Key,
                Balance = g.Sum(w => w.Balance).ToString("F18"),
                LockedAmount = g.Sum(w => w.LockedAmount).ToString("F18"),
                Available = g.Sum(w => w.Balance - w.LockedAmount).ToString("F18"),
            })
            .ToListAsync();

        return Ok(wallets);
    }

    [HttpGet("vault/assets/{assetId}")]
    public async Task<ActionResult<AssetDto>> GetVaultAsset(string assetId)
    {
        var asset = await _context.Assets.FindAsync(assetId);

        if (asset == null)
        {
            throw new KeyNotFoundException($"Asset {assetId} not found");
        }

        var dto = new AssetDto
        {
            Id = asset.AssetId,
            Name = asset.Name,
            Symbol = asset.Symbol,
            Decimals = asset.Decimals,
            Type = asset.Type,
        };

        return Ok(dto);
    }
}
