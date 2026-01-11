using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public VaultAddressesController(FireblocksDbContext context, ILogger<VaultAddressesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<AddressDto>>> GetAddresses(string vaultAccountId, string assetId)
    {
        var wallet = await _context.Wallets
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId);

        if (wallet == null)
        {
            throw new KeyNotFoundException($"Wallet for asset {assetId} not found in vault {vaultAccountId}");
        }

        var addresses = wallet.Addresses.Select(a => new AddressDto
        {
            Address = a.AddressValue,
            Tag = a.Tag,
            Type = a.Type
        }).ToList();

        return Ok(addresses);
    }

    [HttpGet("~/vault/accounts/{vaultAccountId}/{assetId}/addresses_paginated")]
    public async Task<ActionResult<object>> GetAddressesPaginated(
        string vaultAccountId,
        string assetId,
        [FromQuery] int limit = 100,
        [FromQuery] string? before = null,
        [FromQuery] string? after = null)
    {
        var wallet = await _context.Wallets
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId);

        if (wallet == null)
        {
            throw new KeyNotFoundException($"Wallet for asset {assetId} not found in vault {vaultAccountId}");
        }

        // Simple pagination without actual cursor implementation
        var addresses = wallet.Addresses
            .Take(limit)
            .Select(a => new AddressDto
            {
                Address = a.AddressValue,
                Tag = a.Tag,
                Type = a.Type
            }).ToList();

        var response = new
        {
            addresses,
            paging = new PagingDto()
        };

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<AddressDto>> CreateAddress(
        string vaultAccountId,
        string assetId,
        [FromBody] CreateAddressRequestDto? request = null)
    {
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId);

        if (wallet == null)
        {
            throw new KeyNotFoundException($"Wallet for asset {assetId} not found in vault {vaultAccountId}");
        }

        // Generate a mock address based on asset type
        var addressValue = GenerateMockAddress(assetId);

        var address = new Address
        {
            AddressValue = addressValue,
            Tag = request?.Tag,
            Type = request?.Type ?? "Deposit",
            WalletId = wallet.Id,
            CreatedAt = DateTime.UtcNow
        };

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created address {Address} for asset {AssetId} in vault {VaultAccountId}",
            addressValue, assetId, vaultAccountId);

        var dto = new AddressDto
        {
            Address = address.AddressValue,
            Tag = address.Tag,
            Type = address.Type
        };

        return Ok(dto);
    }

    private string GenerateMockAddress(string assetId)
    {
        // Generate mock addresses based on asset type
        return assetId switch
        {
            "BTC" => $"bc1q{Guid.NewGuid().ToString("N")[..40]}",
            "ETH" => $"0x{Guid.NewGuid().ToString("N")[..40]}",
            "USDT" => $"0x{Guid.NewGuid().ToString("N")[..40]}",
            "USDC" => $"0x{Guid.NewGuid().ToString("N")[..40]}",
            _ => $"{assetId}_{Guid.NewGuid():N}"
        };
    }
}

public class CreateAddressRequestDto
{
    public string? Tag { get; set; }
    public string? Type { get; set; }
}
