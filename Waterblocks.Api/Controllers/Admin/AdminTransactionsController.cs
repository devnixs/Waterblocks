using Microsoft.AspNetCore.Mvc;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Services;

namespace Waterblocks.Api.Controllers.Admin;

[ApiController]
[Route("admin/transactions")]
public class AdminTransactionsController : AdminControllerBase
{
    private readonly IAdminTransactionService _transactionService;

    public AdminTransactionsController(
        IAdminTransactionService transactionService,
        WorkspaceContext workspace)
        : base(workspace)
    {
        _transactionService = transactionService;
    }

    [HttpGet]
    public async Task<ActionResult<AdminResponse<List<AdminTransactionDto>>>> GetTransactions()
    {
        return (await _transactionService.GetTransactionsAsync()).ToActionResult(this);
    }

    [HttpGet("paged")]
    public async Task<ActionResult<AdminResponse<AdminTransactionsPageDto>>> GetTransactionsPaged(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? assetId = null,
        [FromQuery] string? transactionId = null,
        [FromQuery] string? hash = null)
    {
        return (await _transactionService.GetTransactionsPagedAsync(pageIndex, pageSize, assetId, transactionId, hash))
            .ToActionResult(this);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AdminResponse<AdminTransactionDto>>> GetTransaction(string id)
    {
        return (await _transactionService.GetTransactionAsync(id)).ToActionResult(this);
    }

    [HttpPost]
    public async Task<ActionResult<AdminResponse<AdminTransactionDto>>> CreateTransaction(
        [FromBody] CreateAdminTransactionRequestDto request)
    {
        return (await _transactionService.CreateTransactionAsync(request)).ToActionResult(this);
    }

    // Positive State Transitions
    [HttpPost("{id}/approve")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> ApproveTransaction(string id)
    {
        return (await _transactionService.ApproveAsync(id)).ToActionResult(this);
    }

    [HttpPost("{id}/sign")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> SignTransaction(string id)
    {
        return (await _transactionService.SignAsync(id)).ToActionResult(this);
    }

    [HttpPost("{id}/broadcast")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> BroadcastTransaction(string id)
    {
        return (await _transactionService.BroadcastAsync(id)).ToActionResult(this);
    }

    [HttpPost("{id}/confirm")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> ConfirmTransaction(string id)
    {
        return (await _transactionService.ConfirmAsync(id)).ToActionResult(this);
    }

    [HttpPost("{id}/complete")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> CompleteTransaction(string id)
    {
        return (await _transactionService.CompleteAsync(id)).ToActionResult(this);
    }

    // Failure Simulation Endpoints
    [HttpPost("{id}/fail")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> FailTransaction(
        string id,
        [FromBody] FailTransactionRequestDto? request = null)
    {
        return (await _transactionService.FailAsync(id, request?.Reason)).ToActionResult(this);
    }

    [HttpPost("{id}/reject")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> RejectTransaction(string id)
    {
        return (await _transactionService.RejectAsync(id)).ToActionResult(this);
    }

    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> CancelTransaction(string id)
    {
        return (await _transactionService.CancelAsync(id)).ToActionResult(this);
    }

    [HttpPost("{id}/timeout")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> TimeoutTransaction(string id)
    {
        return (await _transactionService.TimeoutAsync(id)).ToActionResult(this);
    }
}
