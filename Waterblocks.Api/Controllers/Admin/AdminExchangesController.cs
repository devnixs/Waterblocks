using Microsoft.AspNetCore.Mvc;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Services;

namespace Waterblocks.Api.Controllers.Admin;

/// <summary>
/// Manages the exchange catalog. A new exchange is a new row here, with no code change.
/// </summary>
[ApiController]
[Route("admin/exchanges")]
public class AdminExchangesController : ControllerBase
{
    private readonly IExchangeCatalog _catalog;

    public AdminExchangesController(IExchangeCatalog catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public Task<ActionResult<AdminResponse<List<AdminExchangeDto>>>> GetExchanges()
    {
        return ExecuteAsync(() => _catalog.ListForAdminAsync());
    }

    [HttpPost]
    public Task<ActionResult<AdminResponse<AdminExchangeDto>>> CreateExchange([FromBody] SaveAdminExchangeRequestDto request)
    {
        return ExecuteAsync(() => _catalog.CreateAsync(request));
    }

    [HttpPut("{exchangeType}")]
    public Task<ActionResult<AdminResponse<AdminExchangeDto>>> UpdateExchange(
        string exchangeType,
        [FromBody] SaveAdminExchangeRequestDto request)
    {
        return ExecuteAsync(() => _catalog.UpdateAsync(exchangeType, request));
    }

    private async Task<ActionResult<AdminResponse<T>>> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return Ok(AdminResponse<T>.Success(await operation()));
        }
        catch (ExchangeOperationException exception)
        {
            return StatusCode((int)exception.StatusCode, AdminResponse<T>.Failure(exception.Message, exception.Code));
        }
    }
}
