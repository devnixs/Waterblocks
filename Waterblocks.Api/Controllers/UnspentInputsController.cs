using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Dtos.Fireblocks;

namespace Waterblocks.Api.Controllers;

[ApiController]
[Route("vault/accounts/{vaultAccountId}/{assetId}/unspent_inputs")]
public class UnspentInputsController : ControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<UnspentInputsController> _logger;

    public UnspentInputsController(FireblocksDbContext context, ILogger<UnspentInputsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<UnspentInputsResponseDto>>> GetUnspentInputs(
        string vaultAccountId,
        string assetId)
    {
        var wallet = await _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId);

        if (wallet == null)
        {
            throw new KeyNotFoundException($"Wallet for asset {assetId} not found in vault {vaultAccountId}");
        }

        _logger.LogInformation(
            "Returning unspent inputs for vault {VaultAccountId} asset {AssetId}",
            vaultAccountId,
            assetId);

        return Ok(new List<UnspentInputsResponseDto>());
    }
}
