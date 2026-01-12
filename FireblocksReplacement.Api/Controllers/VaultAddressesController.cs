using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FireblocksReplacement.Api.Infrastructure;
using FireblocksReplacement.Api.Infrastructure.Db;
using FireblocksReplacement.Api.Models;
using FireblocksReplacement.Api.Dtos.Fireblocks;

namespace FireblocksReplacement.Api.Controllers;

[ApiController]
[Route("vault/accounts/{vaultAccountId}/{assetId}/addresses")]
public class VaultAddressesController : ControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<VaultAddressesController> _logger;
    private readonly WorkspaceContext _workspace;

    public VaultAddressesController(
        FireblocksDbContext context,
        ILogger<VaultAddressesController> logger,
        WorkspaceContext workspace)
    {
        _context = context;
        _logger = logger;
        _workspace = workspace;
    }

    [HttpGet]
    public async Task<ActionResult<List<VaultWalletAddressDto>>> GetAddresses(string vaultAccountId, string assetId)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        var wallet = await _context.Wallets
            .Include(w => w.VaultAccount)
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId && w.VaultAccount.WorkspaceId == _workspace.WorkspaceId);

        if (wallet == null)
        {
            throw new KeyNotFoundException($"Wallet for asset {assetId} not found in vault {vaultAccountId}");
        }

        var addresses = wallet.Addresses.Select(a => new VaultWalletAddressDto
        {
            AssetId = assetId,
            Address = a.AddressValue,
            Description = a.Description,
            Tag = a.Tag,
            Type = a.Type,
            CustomerRefId = a.CustomerRefId,
            AddressFormat = a.AddressFormat,
            LegacyAddress = a.LegacyAddress,
            EnterpriseAddress = a.EnterpriseAddress,
            Bip44AddressIndex = a.Bip44AddressIndex
        }).ToList();

        return Ok(addresses);
    }

    [HttpGet("~/vault/accounts/{vaultAccountId}/{assetId}/addresses_paginated")]
    public async Task<ActionResult<PaginatedAddressResponseDto>> GetAddressesPaginated(
        string vaultAccountId,
        string assetId,
        [FromQuery] int limit = 100,
        [FromQuery] string? before = null,
        [FromQuery] string? after = null)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        var wallet = await _context.Wallets
            .Include(w => w.VaultAccount)
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId && w.VaultAccount.WorkspaceId == _workspace.WorkspaceId);

        if (wallet == null)
        {
            throw new KeyNotFoundException($"Wallet for asset {assetId} not found in vault {vaultAccountId}");
        }

        // Simple pagination without actual cursor implementation
        var allAddresses = wallet.Addresses.OrderBy(a => a.Id).ToList();

        // Apply cursor-based pagination
        IEnumerable<Address> filteredAddresses = allAddresses;

        if (!string.IsNullOrEmpty(after))
        {
            // Parse the after cursor (using address ID as cursor)
            if (int.TryParse(after, out var afterId))
            {
                filteredAddresses = filteredAddresses.Where(a => a.Id > afterId);
            }
        }

        if (!string.IsNullOrEmpty(before))
        {
            // Parse the before cursor (using address ID as cursor)
            if (int.TryParse(before, out var beforeId))
            {
                filteredAddresses = filteredAddresses.Where(a => a.Id < beforeId);
            }
        }

        var addresses = filteredAddresses
            .Take(limit)
            .Select(a => new VaultWalletAddressDto
            {
                AssetId = assetId,
                Address = a.AddressValue,
                Description = a.Description,
                Tag = a.Tag,
                Type = a.Type,
                CustomerRefId = a.CustomerRefId,
                AddressFormat = a.AddressFormat,
                LegacyAddress = a.LegacyAddress,
                EnterpriseAddress = a.EnterpriseAddress,
                Bip44AddressIndex = a.Bip44AddressIndex
            }).ToList();

        // Calculate pagination cursors
        var paging = new PagingDto
        {
            Before = addresses.Count > 0 ? addresses.First().Bip44AddressIndex?.ToString() : null,
            After = addresses.Count > 0 && addresses.Count == limit
                ? addresses.Last().Bip44AddressIndex?.ToString()
                : null
        };

        var response = new PaginatedAddressResponseDto
        {
            Addresses = addresses,
            Paging = paging
        };

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<CreateAddressResponseDto>> CreateAddress(
        string vaultAccountId,
        string assetId,
        [FromBody] CreateAddressRequestDto? request = null)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        var wallet = await _context.Wallets
            .Include(w => w.VaultAccount)
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId && w.VaultAccount.WorkspaceId == _workspace.WorkspaceId);

        if (wallet == null)
        {
            throw new KeyNotFoundException($"Wallet for asset {assetId} not found in vault {vaultAccountId}");
        }

        // Determine address format based on asset type
        var addressFormat = DetermineAddressFormat(assetId);

        // Generate address and legacy/enterprise variants
        var addressValue = GenerateAddress(assetId, addressFormat);
        var legacyAddress = GenerateLegacyAddress(assetId, addressFormat);
        var enterpriseAddress = GenerateEnterpriseAddress(assetId);

        // Calculate BIP44 address index (count of existing addresses)
        var bip44AddressIndex = wallet.Addresses.Count;
        var isFirstAddress = bip44AddressIndex == 0;

        var address = new Address
        {
            AddressValue = addressValue,
            Tag = null,
            Type = isFirstAddress ? "Permanent" : "DEPOSIT",
            Description = request?.Description,
            CustomerRefId = request?.CustomerRefId,
            AddressFormat = addressFormat,
            LegacyAddress = legacyAddress,
            EnterpriseAddress = enterpriseAddress,
            Bip44AddressIndex = bip44AddressIndex,
            WalletId = wallet.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created address {Address} for asset {AssetId} in vault {VaultAccountId}",
            addressValue, assetId, vaultAccountId);

        var response = new CreateAddressResponseDto
        {
            Address = address.AddressValue,
            LegacyAddress = address.LegacyAddress,
            EnterpriseAddress = address.EnterpriseAddress,
            Tag = address.Tag,
            Bip44AddressIndex = address.Bip44AddressIndex
        };

        return Ok(response);
    }

    private string DetermineAddressFormat(string assetId)
    {
        return assetId.ToUpperInvariant() switch
        {
            "BTC" => "SEGWIT",
            "ETH" or "USDT" or "USDC" => "BASE",
            _ => "BASE"
        };
    }

    private string GenerateAddress(string assetId, string addressFormat)
    {
        return assetId.ToUpperInvariant() switch
        {
            "BTC" when addressFormat == "SEGWIT" => $"bc1q{Guid.NewGuid():N}"[..42],
            "BTC" => $"1{Guid.NewGuid():N}"[..34],
            "ETH" or "USDT" or "USDC" => $"0x{Guid.NewGuid():N}{Guid.NewGuid():N}"[..42],
            _ => $"{assetId.ToLowerInvariant()}_{Guid.NewGuid():N}"
        };
    }

    private string? GenerateLegacyAddress(string assetId, string addressFormat)
    {
        // Only generate legacy address for BTC when using SEGWIT
        if (assetId.ToUpperInvariant() == "BTC" && addressFormat == "SEGWIT")
        {
            return $"1{Guid.NewGuid():N}"[..34];
        }
        return null;
    }

    private string? GenerateEnterpriseAddress(string assetId)
    {
        // Enterprise addresses are optional and only for certain assets
        return assetId.ToUpperInvariant() switch
        {
            "ETH" or "USDT" or "USDC" => $"0xE{Guid.NewGuid():N}{Guid.NewGuid():N}"[..42],
            _ => null
        };
    }
}
