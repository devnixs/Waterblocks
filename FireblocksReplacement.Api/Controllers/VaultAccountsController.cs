using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FireblocksReplacement.Api.Infrastructure;
using FireblocksReplacement.Api.Infrastructure.Db;
using FireblocksReplacement.Api.Models;
using FireblocksReplacement.Api.Dtos.Fireblocks;

namespace FireblocksReplacement.Api.Controllers;

[ApiController]
[Route("vault/accounts")]
public class VaultAccountsController : ControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<VaultAccountsController> _logger;
    private readonly WorkspaceContext _workspace;

    public VaultAccountsController(
        FireblocksDbContext context,
        ILogger<VaultAccountsController> logger,
        WorkspaceContext workspace)
    {
        _context = context;
        _logger = logger;
        _workspace = workspace;
    }

    [HttpPost]
    public async Task<ActionResult<VaultAccountDto>> CreateVaultAccount([FromBody] CreateVaultAccountRequestDto request)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        var vaultAccount = new VaultAccount
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            CustomerRefId = request.CustomerRefId,
            AutoFuel = request.AutoFuel,
            HiddenOnUI = false,
            WorkspaceId = _workspace.WorkspaceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.VaultAccounts.Add(vaultAccount);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created vault account {VaultAccountId} with name {Name}", vaultAccount.Id, vaultAccount.Name);

        return Ok(MapToDto(vaultAccount));
    }

    [HttpGet]
    public async Task<ActionResult<List<VaultAccountDto>>> GetAllVaultAccounts()
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        var vaultAccounts = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == _workspace.WorkspaceId)
            .Include(v => v.Wallets)
            .ToListAsync();

        var dtos = vaultAccounts.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    [HttpGet("~/vault/accounts_paged")]
    public async Task<ActionResult<VaultAccountsPagedResponseDto>> GetVaultAccountsPaged(
        [FromQuery] string? namePrefix,
        [FromQuery] string? nameSuffix,
        [FromQuery] string? assetId,
        [FromQuery] int limit = 100,
        [FromQuery] string? before = null,
        [FromQuery] string? after = null)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        var query = _context.VaultAccounts
            .Where(v => v.WorkspaceId == _workspace.WorkspaceId)
            .Include(v => v.Wallets)
            .AsQueryable();

        if (!string.IsNullOrEmpty(namePrefix))
        {
            query = query.Where(v => v.Name.StartsWith(namePrefix));
        }

        if (!string.IsNullOrEmpty(nameSuffix))
        {
            query = query.Where(v => v.Name.EndsWith(nameSuffix));
        }

        if (!string.IsNullOrEmpty(assetId))
        {
            query = query.Where(v => v.Wallets.Any(w => w.AssetId == assetId));
        }

        // Simple pagination without actual cursor implementation for now
        var vaultAccounts = await query.Take(limit).ToListAsync();

        var response = new VaultAccountsPagedResponseDto
        {
            Accounts = vaultAccounts.Select(MapToDto).ToList(),
            Paging = new PagingDto()
        };

        return Ok(response);
    }

    [HttpGet("{vaultAccountId}")]
    public async Task<ActionResult<VaultAccountDto>> GetVaultAccount(string vaultAccountId)
    {
        var vaultAccount = await _context.VaultAccounts
            .Include(v => v.Wallets)
            .FirstOrDefaultAsync(v => v.Id == vaultAccountId && v.WorkspaceId == _workspace.WorkspaceId);

        if (vaultAccount == null)
        {
            throw new KeyNotFoundException($"Vault account {vaultAccountId} not found");
        }

        return Ok(MapToDto(vaultAccount));
    }

    [HttpPut("{vaultAccountId}")]
    public async Task<ActionResult<VaultAccountDto>> UpdateVaultAccount(
        string vaultAccountId,
        [FromBody] UpdateVaultAccountRequestDto request)
    {
        var vaultAccount = await _context.VaultAccounts
            .Include(v => v.Wallets)
            .FirstOrDefaultAsync(v => v.Id == vaultAccountId && v.WorkspaceId == _workspace.WorkspaceId);

        if (vaultAccount == null)
        {
            throw new KeyNotFoundException($"Vault account {vaultAccountId} not found");
        }

        vaultAccount.Name = request.Name;
        vaultAccount.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated vault account {VaultAccountId}", vaultAccountId);

        return Ok(MapToDto(vaultAccount));
    }

    [HttpPost("{vaultAccountId}/hide")]
    public async Task<ActionResult<VaultAccountDto>> HideVaultAccount(string vaultAccountId)
    {
        var vaultAccount = await _context.VaultAccounts
            .Include(v => v.Wallets)
            .FirstOrDefaultAsync(v => v.Id == vaultAccountId && v.WorkspaceId == _workspace.WorkspaceId);

        if (vaultAccount == null)
        {
            throw new KeyNotFoundException($"Vault account {vaultAccountId} not found");
        }

        vaultAccount.HiddenOnUI = true;
        vaultAccount.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Hid vault account {VaultAccountId}", vaultAccountId);

        return Ok(MapToDto(vaultAccount));
    }

    [HttpPost("{vaultAccountId}/unhide")]
    public async Task<ActionResult<VaultAccountDto>> UnhideVaultAccount(string vaultAccountId)
    {
        var vaultAccount = await _context.VaultAccounts
            .Include(v => v.Wallets)
            .FirstOrDefaultAsync(v => v.Id == vaultAccountId && v.WorkspaceId == _workspace.WorkspaceId);

        if (vaultAccount == null)
        {
            throw new KeyNotFoundException($"Vault account {vaultAccountId} not found");
        }

        vaultAccount.HiddenOnUI = false;
        vaultAccount.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Unhid vault account {VaultAccountId}", vaultAccountId);

        return Ok(MapToDto(vaultAccount));
    }

    private VaultAccountDto MapToDto(VaultAccount vaultAccount)
    {
        return new VaultAccountDto
        {
            Id = vaultAccount.Id,
            Name = vaultAccount.Name,
            HiddenOnUI = vaultAccount.HiddenOnUI,
            CustomerRefId = vaultAccount.CustomerRefId,
            AutoFuel = vaultAccount.AutoFuel,
            Assets = vaultAccount.Wallets.Select(w => new VaultAssetDto
            {
                Id = w.AssetId,
                Balance = w.Balance.ToString("F18"),
                LockedAmount = w.LockedAmount.ToString("F18"),
                Available = (w.Balance - w.LockedAmount).ToString("F18")
            }).ToList()
        };
    }
}
