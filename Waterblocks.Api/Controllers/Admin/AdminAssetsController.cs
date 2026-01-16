using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Controllers.Admin;

[ApiController]
[Route("admin/assets")]
public class AdminAssetsController : AdminControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<AdminAssetsController> _logger;

    public AdminAssetsController(
        FireblocksDbContext context,
        ILogger<AdminAssetsController> logger,
        WorkspaceContext workspace)
        : base(workspace)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<AdminResponse<List<AdminAssetDto>>>> GetAssets()
    {
        var assets = await _context.Assets
            .OrderBy(a => a.AssetId)
            .ToListAsync();

        var dtos = assets.Select(MapToDto).ToList();
        return Ok(AdminResponse<List<AdminAssetDto>>.Success(dtos));
    }

    [HttpPost]
    public async Task<ActionResult<AdminResponse<AdminAssetDto>>> CreateAsset(
        [FromBody] CreateAdminAssetRequestDto request)
    {
        var assetId = NormalizeId(request.AssetId);
        if (string.IsNullOrEmpty(assetId))
        {
            return BadRequest(AdminResponse<AdminAssetDto>.Failure("Asset ID is required", "ASSET_ID_REQUIRED"));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(AdminResponse<AdminAssetDto>.Failure("Asset name is required", "NAME_REQUIRED"));
        }

        var symbol = NormalizeId(request.Symbol);
        if (string.IsNullOrEmpty(symbol))
        {
            return BadRequest(AdminResponse<AdminAssetDto>.Failure("Symbol is required", "SYMBOL_REQUIRED"));
        }

        if (await _context.Assets.AnyAsync(a => a.AssetId == assetId))
        {
            return BadRequest(AdminResponse<AdminAssetDto>.Failure($"Asset {assetId} already exists", "ASSET_ALREADY_EXISTS"));
        }

        if (!TryParseBlockchainType(request.BlockchainType, out var blockchainType, out var errorMessage))
        {
            return BadRequest(AdminResponse<AdminAssetDto>.Failure(errorMessage, "BLOCKCHAIN_TYPE_INVALID"));
        }

        var decimals = request.Decimals ?? 18;
        if (decimals < 0)
        {
            return BadRequest(AdminResponse<AdminAssetDto>.Failure("Decimals must be zero or greater", "DECIMALS_INVALID"));
        }

        var asset = new Asset
        {
            AssetId = assetId,
            Name = request.Name.Trim(),
            Symbol = symbol,
            Decimals = decimals,
            Type = NormalizeOptionalText(request.Type),
            BlockchainType = blockchainType,
            ContractAddress = NormalizeOptionalText(request.ContractAddress),
            NativeAsset = NormalizeOptionalId(request.NativeAsset),
            BaseFee = request.BaseFee ?? 0m,
            FeeAssetId = NormalizeOptionalId(request.FeeAssetId),
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        if (asset.BaseFee < 0)
        {
            return BadRequest(AdminResponse<AdminAssetDto>.Failure("Base fee must be zero or greater", "BASE_FEE_INVALID"));
        }

        _context.Assets.Add(asset);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created asset {AssetId}", asset.AssetId);

        return Ok(AdminResponse<AdminAssetDto>.Success(MapToDto(asset)));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<AdminResponse<AdminAssetDto>>> UpdateAsset(
        string id,
        [FromBody] UpdateAdminAssetRequestDto request)
    {
        var assetId = NormalizeId(id);
        if (string.IsNullOrEmpty(assetId))
        {
            return BadRequest(AdminResponse<AdminAssetDto>.Failure("Asset ID is required", "ASSET_ID_REQUIRED"));
        }

        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.AssetId == assetId);
        if (asset == null)
        {
            return NotFound(AdminResponse<AdminAssetDto>.Failure($"Asset {assetId} not found", "ASSET_NOT_FOUND"));
        }

        if (request.Name != null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(AdminResponse<AdminAssetDto>.Failure("Asset name is required", "NAME_REQUIRED"));
            }

            asset.Name = request.Name.Trim();
        }

        if (request.Symbol != null)
        {
            var symbol = NormalizeId(request.Symbol);
            if (string.IsNullOrEmpty(symbol))
            {
                return BadRequest(AdminResponse<AdminAssetDto>.Failure("Symbol is required", "SYMBOL_REQUIRED"));
            }

            asset.Symbol = symbol;
        }

        if (request.Decimals.HasValue)
        {
            if (request.Decimals.Value < 0)
            {
                return BadRequest(AdminResponse<AdminAssetDto>.Failure("Decimals must be zero or greater", "DECIMALS_INVALID"));
            }

            asset.Decimals = request.Decimals.Value;
        }

        if (request.BlockchainType != null)
        {
            if (!TryParseBlockchainType(request.BlockchainType, out var blockchainType, out var errorMessage))
            {
                return BadRequest(AdminResponse<AdminAssetDto>.Failure(errorMessage, "BLOCKCHAIN_TYPE_INVALID"));
            }

            asset.BlockchainType = blockchainType;
        }

        if (request.Type != null)
        {
            asset.Type = NormalizeOptionalText(request.Type);
        }

        if (request.ContractAddress != null)
        {
            asset.ContractAddress = NormalizeOptionalText(request.ContractAddress);
        }

        if (request.NativeAsset != null)
        {
            asset.NativeAsset = NormalizeOptionalId(request.NativeAsset);
        }

        if (request.BaseFee.HasValue)
        {
            if (request.BaseFee.Value < 0)
            {
                return BadRequest(AdminResponse<AdminAssetDto>.Failure("Base fee must be zero or greater", "BASE_FEE_INVALID"));
            }

            asset.BaseFee = request.BaseFee.Value;
        }

        if (request.FeeAssetId != null)
        {
            asset.FeeAssetId = NormalizeOptionalId(request.FeeAssetId);
        }

        if (request.IsActive.HasValue)
        {
            asset.IsActive = request.IsActive.Value;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated asset {AssetId}", asset.AssetId);

        return Ok(AdminResponse<AdminAssetDto>.Success(MapToDto(asset)));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<AdminResponse<bool>>> DeleteAsset(string id)
    {
        var assetId = NormalizeId(id);
        if (string.IsNullOrEmpty(assetId))
        {
            return BadRequest(AdminResponse<bool>.Failure("Asset ID is required", "ASSET_ID_REQUIRED"));
        }

        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.AssetId == assetId);
        if (asset == null)
        {
            return NotFound(AdminResponse<bool>.Failure($"Asset {assetId} not found", "ASSET_NOT_FOUND"));
        }

        asset.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deactivated asset {AssetId}", asset.AssetId);

        return Ok(AdminResponse<bool>.Success(true));
    }

    private static AdminAssetDto MapToDto(Asset asset)
    {
        return new AdminAssetDto
        {
            Id = asset.AssetId,
            Name = asset.Name,
            Symbol = asset.Symbol,
            Decimals = asset.Decimals,
            Type = asset.Type,
            BlockchainType = asset.BlockchainType.ToString(),
            ContractAddress = asset.ContractAddress,
            NativeAsset = asset.NativeAsset,
            BaseFee = asset.BaseFee,
            FeeAssetId = asset.FeeAssetId,
            IsActive = asset.IsActive,
            CreatedAt = asset.CreatedAt,
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (value == null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? NormalizeOptionalId(string? value)
    {
        if (value == null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToUpperInvariant();
    }

    private static string NormalizeId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    }

    private static bool TryParseBlockchainType(string? value, out BlockchainType blockchainType, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            blockchainType = BlockchainType.AccountBased;
            return true;
        }

        if (Enum.TryParse(value.Trim(), ignoreCase: true, out blockchainType))
        {
            return true;
        }

        errorMessage = $"Unknown blockchain type: {value}";
        return false;
    }
}
