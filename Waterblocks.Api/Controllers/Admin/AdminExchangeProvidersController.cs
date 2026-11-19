using Microsoft.AspNetCore.Mvc;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Services;

namespace Waterblocks.Api.Controllers.Admin;

/// <summary>
/// Lists the exchanges that Waterblocks can connect to their simulators.
/// </summary>
[ApiController]
[Route("admin/exchange-providers")]
public class AdminExchangeProvidersController : ControllerBase
{
    private readonly IExchangeAccountService _exchangeAccounts;

    public AdminExchangeProvidersController(IExchangeAccountService exchangeAccounts)
    {
        _exchangeAccounts = exchangeAccounts;
    }

    [HttpGet]
    public ActionResult<AdminResponse<List<AdminExchangeProviderDto>>> GetExchangeProviders()
    {
        return Ok(AdminResponse<List<AdminExchangeProviderDto>>.Success(_exchangeAccounts.ListProviders()));
    }
}
