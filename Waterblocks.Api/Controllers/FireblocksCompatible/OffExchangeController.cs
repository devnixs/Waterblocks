using Microsoft.AspNetCore.Mvc;
using Waterblocks.Api.Dtos.Fireblocks;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Models;
using Waterblocks.Api.Services;

namespace Waterblocks.Api.Controllers;

[ApiController]
[Route("off_exchange")]
public class OffExchangeController : ControllerBase
{
    private readonly ICollateralTransferService _collateralTransfers;
    private readonly IExchangeAccountService _exchangeAccounts;
    private readonly WorkspaceContext _workspace;

    public OffExchangeController(
        ICollateralTransferService collateralTransfers,
        IExchangeAccountService exchangeAccounts,
        WorkspaceContext workspace)
    {
        _collateralTransfers = collateralTransfers;
        _exchangeAccounts = exchangeAccounts;
        _workspace = workspace;
    }

    [HttpPost("add")]
    public async Task<ActionResult<CreateTransactionResponseDto>> AddCollateral([FromBody] AddCollateralRequestDto request)
    {
        var transactionRequest = RequireTransactionRequest(request.TransactionRequest);
        if (transactionRequest.Source?.Type != TransferPeerType.VAULT_ACCOUNT ||
            transactionRequest.Destination?.Type != TransferPeerType.EXCHANGE_ACCOUNT)
        {
            throw FireblocksApiException.BadRequest(
                "Add collateral needs a VAULT_ACCOUNT source and an EXCHANGE_ACCOUNT destination");
        }

        var transaction = await _collateralTransfers.AddAsync(BuildCommand(
            transactionRequest,
            exchangeAccountId: transactionRequest.Destination.Id,
            vaultAccountId: transactionRequest.Source.Id));
        return Ok(ToResponse(transaction));
    }

    [HttpPost("remove")]
    public async Task<ActionResult<CreateTransactionResponseDto>> RemoveCollateral([FromBody] RemoveCollateralRequestDto request)
    {
        var transactionRequest = RequireTransactionRequest(request.TransactionRequest);
        if (transactionRequest.Source?.Type != TransferPeerType.EXCHANGE_ACCOUNT ||
            transactionRequest.Destination?.Type != TransferPeerType.VAULT_ACCOUNT)
        {
            throw FireblocksApiException.BadRequest(
                "Remove collateral needs an EXCHANGE_ACCOUNT source and a VAULT_ACCOUNT destination");
        }

        var transaction = await _collateralTransfers.RemoveAsync(BuildCommand(
            transactionRequest,
            exchangeAccountId: transactionRequest.Source.Id,
            vaultAccountId: transactionRequest.Destination.Id));
        return Ok(ToResponse(transaction));
    }

    [HttpGet("collateral_accounts/{mainExchangeAccountId}")]
    public async Task<ActionResult<ExchangeAccountDto>> GetCollateralAccount(string mainExchangeAccountId)
    {
        return Ok(await _exchangeAccounts.GetCollateralAccountAsync(mainExchangeAccountId));
    }

    private static CreateTransactionRequestDto RequireTransactionRequest(CreateTransactionRequestDto? transactionRequest)
    {
        return transactionRequest ?? throw FireblocksApiException.BadRequest("transactionRequest is required");
    }

    private static CollateralTransferCommand BuildCommand(
        CreateTransactionRequestDto request,
        string? exchangeAccountId,
        string? vaultAccountId)
    {
        return new CollateralTransferCommand(
            exchangeAccountId,
            vaultAccountId,
            request.AssetId,
            request.Amount,
            request.TreatAsGrossAmount ?? false,
            request.Note,
            request.ExternalTxId,
            request.CustomerRefId,
            InitiatedBy: null);
    }

    private CreateTransactionResponseDto ToResponse(Transaction transaction)
    {
        return new CreateTransactionResponseDto
        {
            Id = TransactionCompositeId.Build(_workspace.WorkspaceId, transaction.Id),
            Status = transaction.State.ToString(),
        };
    }
}
