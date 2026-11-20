using Microsoft.AspNetCore.Mvc;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Dtos.Fireblocks;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Services;
using Waterblocks.Api.Utils;

namespace Waterblocks.Api.Controllers.Admin;

[ApiController]
[Route("admin/exchange-accounts")]
public class AdminExchangeAccountsController : AdminControllerBase
{
    private readonly IExchangeAccountService _exchangeAccounts;
    private readonly ICollateralTransferService _collateralTransfers;

    public AdminExchangeAccountsController(
        IExchangeAccountService exchangeAccounts,
        ICollateralTransferService collateralTransfers,
        WorkspaceContext workspace)
        : base(workspace)
    {
        _exchangeAccounts = exchangeAccounts;
        _collateralTransfers = collateralTransfers;
    }

    [HttpGet]
    public Task<ActionResult<AdminResponse<List<AdminExchangeAccountDto>>>> GetExchangeAccounts()
    {
        return ExecuteAsync(() => _exchangeAccounts.ListForAdminAsync());
    }

    /// <summary>
    /// The public key that encrypts creds, the same as GET /exchange_accounts/credentials_public_key.
    /// </summary>
    [HttpGet("credentials-public-key")]
    public Task<ActionResult<AdminResponse<ExchangeCredentialsPublicKeyDto>>> GetCredentialsPublicKey()
    {
        return ExecuteAsync(() => Task.FromResult(_exchangeAccounts.GetCredentialsPublicKey()));
    }

    [HttpPost]
    public Task<ActionResult<AdminResponse<AdminExchangeAccountDto>>> CreateExchangeAccount(
        [FromBody] CreateAdminExchangeAccountRequestDto request)
    {
        return ExecuteAsync(() => _exchangeAccounts.CreateConnectedAsync(request));
    }

    [HttpGet("{id}")]
    public Task<ActionResult<AdminResponse<AdminExchangeAccountDto>>> GetExchangeAccount(string id)
    {
        return ExecuteAsync(() => _exchangeAccounts.GetForAdminAsync(id));
    }

    [HttpPost("{id}/connect")]
    public Task<ActionResult<AdminResponse<AdminExchangeAccountDto>>> ConnectExchangeAccount(
        string id,
        [FromBody] ConnectAdminExchangeAccountRequestDto request)
    {
        return ExecuteAsync(() => _exchangeAccounts.ConnectAsync(id, request));
    }

    [HttpPost("{id}/add-collateral")]
    public Task<ActionResult<AdminResponse<AdminCollateralTransferDto>>> AddCollateral(
        string id,
        [FromBody] AdminCollateralTransferRequestDto request)
    {
        return ExecuteAsync(async () =>
        {
            var transaction = await _collateralTransfers.AddAsync(BuildCommand(id, request));
            return await _exchangeAccounts.MapTransferAsync(transaction);
        });
    }

    [HttpPost("{id}/remove-collateral")]
    public Task<ActionResult<AdminResponse<AdminCollateralTransferDto>>> RemoveCollateral(
        string id,
        [FromBody] AdminCollateralTransferRequestDto request)
    {
        return ExecuteAsync(async () =>
        {
            var transaction = await _collateralTransfers.RemoveAsync(BuildCommand(id, request));
            return await _exchangeAccounts.MapTransferAsync(transaction);
        });
    }

    [HttpGet("{id}/transfers")]
    public Task<ActionResult<AdminResponse<List<AdminCollateralTransferDto>>>> GetTransfers(string id)
    {
        return ExecuteAsync(() => _exchangeAccounts.ListTransfersAsync(id));
    }

    [HttpPatch("{id}/assets/{assetId}")]
    public Task<ActionResult<AdminResponse<AdminExchangeAssetDto>>> UpdateAsset(
        string id,
        string assetId,
        [FromBody] UpdateAdminExchangeAssetRequestDto request)
    {
        return ExecuteAsync(() => _exchangeAccounts.UpdateAssetAsync(id, assetId, request));
    }

    private static CollateralTransferCommand BuildCommand(string exchangeAccountId, AdminCollateralTransferRequestDto request)
    {
        return new CollateralTransferCommand(
            exchangeAccountId,
            request.VaultAccountId,
            request.AssetId,
            request.Amount,
            request.TreatAsGrossAmount ?? false,
            request.Note,
            ExternalTxId: null,
            CustomerRefId: null,
            InitiatedBy: string.IsNullOrWhiteSpace(request.InitiatedBy) ? null : request.InitiatedBy.Trim());
    }

    private async Task<ActionResult<AdminResponse<T>>> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (!TryGetWorkspaceId<T>(out _, out var failure))
        {
            return failure;
        }

        try
        {
            return Ok(AdminResponse<T>.Success(await operation()));
        }
        catch (ExchangeOperationException exception)
        {
            return Failure<T>((int)exception.StatusCode, exception.Message, exception.Code);
        }
        catch (DuplicateExternalTxIdException exception)
        {
            return Failure<T>(StatusCodes.Status409Conflict, exception.Message, "DUPLICATE_EXTERNAL_TX_ID");
        }
        catch (KeyNotFoundException exception)
        {
            return Failure<T>(StatusCodes.Status404NotFound, exception.Message, "NOT_FOUND");
        }
        catch (UnsafeNumericValueException exception)
        {
            return Failure<T>(StatusCodes.Status400BadRequest, exception.Message, "UNSAFE_DECIMAL_VALUE");
        }
        catch (ArgumentException exception)
        {
            return Failure<T>(StatusCodes.Status400BadRequest, exception.Message, "INVALID_REQUEST");
        }
    }

    private ActionResult<AdminResponse<T>> Failure<T>(int statusCode, string message, string code)
    {
        return StatusCode(statusCode, AdminResponse<T>.Failure(message, code));
    }
}
