using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Waterblocks.Api.Dtos.Fireblocks;

namespace Waterblocks.Api.Controllers;

[ApiController]
[Route("vault/accounts/{vaultAccountId}/{assetId}/addresses")]
public class VaultAddressesController : ControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<VaultAddressesController> _logger;
    private readonly WorkspaceContext _workspace;
    private readonly Waterblocks.Api.Services.IWalletAddressService _walletAddressService;

    public VaultAddressesController(
        FireblocksDbContext context,
        ILogger<VaultAddressesController> logger,
        WorkspaceContext workspace,
        Waterblocks.Api.Services.IWalletAddressService walletAddressService)
    {
        _context = context;
        _logger = logger;
        _workspace = workspace;
        _walletAddressService = walletAddressService;
    }

    [HttpGet]
    public async Task<ActionResult<List<VaultWalletAddressDto>>> GetAddresses(string vaultAccountId, string assetId)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        var vaultExists = await _context.VaultAccounts
            .AnyAsync(v => v.Id == vaultAccountId && v.WorkspaceId == _workspace.WorkspaceId);
        if (!vaultExists)
        {
            throw new KeyNotFoundException($"Vault account {vaultAccountId} not found");
        }

        var assetExists = await _context.Assets.AnyAsync(a => a.AssetId == assetId);
        if (!assetExists)
        {
            throw new KeyNotFoundException($"Asset {assetId} not found");
        }

        var wallet = await _context.Wallets
            .Include(w => w.VaultAccount)
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId && w.VaultAccount.WorkspaceId == _workspace.WorkspaceId);

        if (wallet == null)
        {
            return Ok(new List<VaultWalletAddressDto>());
        }

        var addresses = wallet.Addresses.Select(a => new VaultWalletAddressDto
        {
            AssetId = assetId,
            Address = a.AddressValue ?? string.Empty,
            Description = a.Description ?? string.Empty,
            Tag = a.Tag ?? string.Empty,
            Type = a.Type ?? string.Empty,
            CustomerRefId = a.CustomerRefId ?? string.Empty,
            AddressFormat = a.AddressFormat ?? "BASE",
            LegacyAddress = a.LegacyAddress ?? string.Empty,
            EnterpriseAddress = a.EnterpriseAddress ?? string.Empty,
            Bip44AddressIndex = a.Bip44AddressIndex ?? 0,
        }).ToList();

        return Ok(addresses);
    }

    [HttpGet("~/vault/accounts/{vaultAccountId}/{assetId}/addresses_paginated")]
    public async Task<ActionResult<PaginatedAddressResponseDto>> GetAddressesPaginated(
        string vaultAccountId,
        string assetId,
        [FromQuery(Name = "count")] int? count = null,
        [FromQuery] int limit = 100,
        [FromQuery] string? before = null,
        [FromQuery] string? after = null)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        var vaultExists = await _context.VaultAccounts
            .AnyAsync(v => v.Id == vaultAccountId && v.WorkspaceId == _workspace.WorkspaceId);
        if (!vaultExists)
        {
            throw new KeyNotFoundException($"Vault account {vaultAccountId} not found");
        }

        var assetExists = await _context.Assets.AnyAsync(a => a.AssetId == assetId);
        if (!assetExists)
        {
            throw new KeyNotFoundException($"Asset {assetId} not found");
        }

        var wallet = await _context.Wallets
            .Include(w => w.VaultAccount)
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId && w.VaultAccount.WorkspaceId == _workspace.WorkspaceId);

        if (wallet == null)
        {
            return Ok(new PaginatedAddressResponseDto
            {
                Addresses = new List<VaultWalletAddressDto>(),
                Paging = new PagingDto
                {
                    Before = string.Empty,
                    After = string.Empty,
                },
            });
        }

        if (wallet.Addresses.Count == 0)
        {
            var asset = await _context.Assets.FindAsync(assetId);
            if (asset == null)
            {
                throw new KeyNotFoundException($"Asset {assetId} not found");
            }

            await _walletAddressService.CreateAddressAsync(wallet, asset, _workspace.WorkspaceId, null, null);

            wallet = await _context.Wallets
                .Include(w => w.VaultAccount)
                .Include(w => w.Addresses)
                .FirstAsync(w => w.Id == wallet.Id);
        }

        var allAddresses = wallet.Addresses
            .OrderBy(a => a.Bip44AddressIndex ?? int.MaxValue)
            .ThenBy(a => a.Id)
            .ToList();

        // Apply cursor-based pagination
        IEnumerable<Address> filteredAddresses = allAddresses;

        if (!string.IsNullOrEmpty(after))
        {
            // Parse the after cursor (using BIP44 index as cursor)
            if (int.TryParse(after, out var afterIndex))
            {
                filteredAddresses = filteredAddresses.Where(a => (a.Bip44AddressIndex ?? int.MaxValue) > afterIndex);
            }
        }

        if (!string.IsNullOrEmpty(before))
        {
            // Parse the before cursor (using BIP44 index as cursor)
            if (int.TryParse(before, out var beforeIndex))
            {
                filteredAddresses = filteredAddresses.Where(a => (a.Bip44AddressIndex ?? int.MaxValue) < beforeIndex);
            }
        }

        var pageSize = count ?? limit;
        var addresses = filteredAddresses
            .Take(pageSize)
            .Select(a => new VaultWalletAddressDto
            {
                AssetId = assetId,
                Address = a.AddressValue ?? string.Empty,
                Description = a.Description ?? string.Empty,
                Tag = a.Tag ?? string.Empty,
                Type = a.Type ?? string.Empty,
                CustomerRefId = a.CustomerRefId ?? string.Empty,
                AddressFormat = a.AddressFormat ?? "BASE",
                LegacyAddress = a.LegacyAddress ?? string.Empty,
                EnterpriseAddress = a.EnterpriseAddress ?? string.Empty,
                Bip44AddressIndex = a.Bip44AddressIndex ?? 0,
            }).ToList();

        // Calculate pagination cursors
        var paging = new PagingDto
        {
            Before = addresses.Count > 0
                ? addresses.First().Bip44AddressIndex.ToString()
                : string.Empty,
            After = addresses.Count > 0 && addresses.Count == pageSize
                ? addresses.Last().Bip44AddressIndex.ToString()
                : string.Empty,
        };

        var response = new PaginatedAddressResponseDto
        {
            Addresses = addresses,
            Paging = paging,
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

        var assetForCreate = await _context.Assets.FindAsync(assetId);
        if (assetForCreate == null)
        {
            throw new KeyNotFoundException($"Asset {assetId} not found");
        }

        var address = await _walletAddressService.CreateAddressAsync(
            wallet,
            assetForCreate,
            _workspace.WorkspaceId,
            request?.Description,
            request?.CustomerRefId);

        var response = new CreateAddressResponseDto
        {
            Address = address.AddressValue ?? string.Empty,
            LegacyAddress = address.LegacyAddress ?? string.Empty,
            EnterpriseAddress = address.EnterpriseAddress ?? string.Empty,
            Tag = address.Tag ?? string.Empty,
            Bip44AddressIndex = address.Bip44AddressIndex ?? 0,
        };

        return Ok(response);
    }

}
