using Microsoft.AspNetCore.Mvc;
using Waterblocks.Api.Dtos.Fireblocks;
using Waterblocks.Api.Services;

namespace Waterblocks.Api.Controllers;

[ApiController]
[Route("exchange_accounts")]
public class ExchangeAccountsController : ControllerBase
{
    private readonly IExchangeAccountService _exchangeAccounts;

    public ExchangeAccountsController(IExchangeAccountService exchangeAccounts)
    {
        _exchangeAccounts = exchangeAccounts;
    }

    [HttpPost]
    public async Task<ActionResult<AddExchangeAccountResponseDto>> AddExchangeAccount(
        [FromBody] AddExchangeAccountRequestDto request)
    {
        var response = await _exchangeAccounts.CreateAsync(request);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    public async Task<ActionResult<List<ExchangeAccountDto>>> GetExchangeAccounts()
    {
        return Ok(await _exchangeAccounts.ListAsync());
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedExchangeAccountsResponseDto>> GetPagedExchangeAccounts(
        [FromQuery] int limit = 3,
        [FromQuery] string? before = null,
        [FromQuery] string? after = null)
    {
        return Ok(await _exchangeAccounts.ListPagedAsync(limit, before, after));
    }

    [HttpGet("credentials_public_key")]
    public ActionResult<ExchangeCredentialsPublicKeyDto> GetCredentialsPublicKey()
    {
        return Ok(_exchangeAccounts.GetCredentialsPublicKey());
    }

    [HttpGet("{exchangeAccountId}")]
    public async Task<ActionResult<ExchangeAccountDto>> GetExchangeAccount(string exchangeAccountId)
    {
        return Ok(await _exchangeAccounts.GetAsync(exchangeAccountId));
    }

    [HttpGet("{exchangeAccountId}/{assetId}")]
    public async Task<ActionResult<ExchangeAssetDto>> GetExchangeAccountAsset(string exchangeAccountId, string assetId)
    {
        return Ok(await _exchangeAccounts.GetAssetAsync(exchangeAccountId, assetId));
    }
}
