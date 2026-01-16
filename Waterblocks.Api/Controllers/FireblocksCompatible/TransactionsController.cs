using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Waterblocks.Api.Dtos.Fireblocks;
using Waterblocks.Api.Services;

namespace Waterblocks.Api.Controllers;

[ApiController]
[Route("transactions")]
public class TransactionsController : ControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<TransactionsController> _logger;
    private readonly WorkspaceContext _workspace;
    private readonly ITransactionService _transactionService;
    private readonly ITransactionViewService _transactionView;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly ITransactionIdResolver _transactionIdResolver;

    public TransactionsController(
        FireblocksDbContext context,
        ILogger<TransactionsController> logger,
        WorkspaceContext workspace,
        ITransactionService transactionService,
        ITransactionViewService transactionView,
        IRealtimeNotifier realtimeNotifier,
        ITransactionIdResolver transactionIdResolver)
    {
        _context = context;
        _logger = logger;
        _workspace = workspace;
        _transactionService = transactionService;
        _transactionView = transactionView;
        _realtimeNotifier = realtimeNotifier;
        _transactionIdResolver = transactionIdResolver;
    }

    [HttpPost]
    public async Task<ActionResult<CreateTransactionResponseDto>> CreateTransaction([FromBody] CreateTransactionRequestDto request)
    {
        var response = await _transactionService.CreateTransactionAsync(request);
        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<List<TransactionDto>>> GetTransactions(
        [FromQuery] string? status = null,
        [FromQuery] int limit = 200,
        [FromQuery] string? before = null,
        [FromQuery] string? after = null)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw FireblocksApiException.Unauthorized("Workspace is required");
        }

        // Filter by address ownership instead of WorkspaceId for cross-workspace support
        var workspaceAddresses = await _transactionView.GetWorkspaceAddressesAsync(_workspace.WorkspaceId!);

        var query = _transactionView.ApplyWorkspaceAddressFilter(
                _context.Transactions.Include(t => t.VaultAccount),
                workspaceAddresses)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<TransactionState>(status, out var stateFilter))
        {
            query = query.Where(t => t.State == stateFilter);
        }

        if (!string.IsNullOrWhiteSpace(before))
        {
            if (!long.TryParse(before, out var beforeMs))
            {
                throw FireblocksApiException.BadRequest($"Invalid before parameter: {before}");
            }

            var beforeDate = DateTimeOffset.FromUnixTimeMilliseconds(beforeMs);
            query = query.Where(t => t.CreatedAt < beforeDate);
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            if (!long.TryParse(after, out var afterMs))
            {
                throw FireblocksApiException.BadRequest($"Invalid after parameter: {after}");
            }

            var afterDate = DateTimeOffset.FromUnixTimeMilliseconds(afterMs);
            query = query.Where(t => t.CreatedAt > afterDate);
        }

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var addressLookup = await _transactionView.BuildAddressOwnershipLookupAsync(transactions, _workspace.WorkspaceId!);
        var dtos = transactions.Select(t => _transactionView.MapToFireblocksDto(t, addressLookup, _workspace.WorkspaceId)).ToList();
        return Ok(dtos);
    }

    [HttpGet("{txId}")]
    public async Task<ActionResult<TransactionDto>> GetTransaction(string txId)
    {
        var transaction = await RequireTransactionAsync(txId, includeVaultAccount: true);

        var addressLookup = await _transactionView.BuildAddressOwnershipLookupAsync(new[] { transaction }, _workspace.WorkspaceId!);
        return Ok(_transactionView.MapToFireblocksDto(transaction, addressLookup, _workspace.WorkspaceId));
    }

    [HttpGet("external_tx_id/{externalTxId}")]
    public async Task<ActionResult<TransactionDto>> GetTransactionByExternalId(string externalTxId)
    {
        // Filter by address ownership instead of WorkspaceId for cross-workspace support
        var workspaceAddresses = await _transactionView.GetWorkspaceAddressesAsync(_workspace.WorkspaceId!);

        var transaction = await _transactionView.ApplyWorkspaceAddressFilter(
                _context.Transactions.Include(t => t.VaultAccount),
                workspaceAddresses)
            .FirstOrDefaultAsync(t => t.ExternalTxId == externalTxId);

        if (transaction == null)
        {
            throw FireblocksApiException.NotFound($"Transaction with external ID {externalTxId} not found");
        }

        var addressLookup = await _transactionView.BuildAddressOwnershipLookupAsync(new[] { transaction }, _workspace.WorkspaceId!);
        return Ok(_transactionView.MapToFireblocksDto(transaction, addressLookup, _workspace.WorkspaceId));
    }

    [HttpPost("{txId}/cancel")]
    public async Task<ActionResult<CancelTransactionResponseDto>> CancelTransaction(string txId)
    {
        var transaction = await FindTransactionByIdOrExternalIdAsync(txId);

        if (transaction.State.IsTerminal())
        {
            throw FireblocksApiException.BadRequest($"Cannot cancel transaction in {transaction.State} state");
        }

        transaction.TransitionTo(TransactionState.CANCELLED);
        transaction.SubStatus = "CANCELLED_BY_USER";
        await _context.SaveChangesAsync();

        await NotifyTransactionsUpdatedAsync(transaction);

        _logger.LogInformation("Cancelled transaction {TransactionId}", txId);

        return Ok(new CancelTransactionResponseDto { Success = true });
    }

    [HttpPost("{txId}/freeze")]
    public async Task<ActionResult<FreezeTransactionResponseDto>> FreezeTransaction(string txId)
    {
        var transaction = await FindTransactionByIdOrExternalIdAsync(txId);

        transaction.Freeze();
        await _context.SaveChangesAsync();

        await NotifyTransactionsUpdatedAsync(transaction);

        _logger.LogInformation("Froze transaction {TransactionId}", txId);

        return Ok(new FreezeTransactionResponseDto { Success = true });
    }

    [HttpPost("{txId}/unfreeze")]
    public async Task<ActionResult<FreezeTransactionResponseDto>> UnfreezeTransaction(string txId)
    {
        var transaction = await FindTransactionByIdOrExternalIdAsync(txId);

        transaction.Unfreeze();
        await _context.SaveChangesAsync();

        await NotifyTransactionsUpdatedAsync(transaction);

        _logger.LogInformation("Unfroze transaction {TransactionId}", txId);

        return Ok(new FreezeTransactionResponseDto { Success = true });
    }

    [HttpPost("{txId}/drop")]
    public async Task<ActionResult<DropTransactionResponseDto>> DropTransaction(string txId, [FromBody] CreateTransactionRequestDto? replacementRequest = null)
    {
        var transaction = await FindTransactionByIdOrExternalIdAsync(txId);

        // Only ETH transactions can be dropped
        if (transaction.AssetId != "ETH")
        {
            throw FireblocksApiException.BadRequest("Drop operation only supported for ETH transactions");
        }

        if (transaction.State.IsTerminal())
        {
            throw FireblocksApiException.BadRequest($"Cannot drop transaction in {transaction.State} state");
        }

        // Mark original as replaced
        transaction.State = TransactionState.CANCELLED;
        transaction.SubStatus = "DROPPED_BY_BLOCKCHAIN";
        transaction.FailureReason = "Replaced by higher fee transaction";

        // Create replacement transaction with higher fee
        var replacement = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            VaultAccountId = transaction.VaultAccountId,
            WorkspaceId = transaction.WorkspaceId,
            AssetId = transaction.AssetId,
            Amount = transaction.Amount,
            RequestedAmount = transaction.RequestedAmount,
            DestinationAddress = transaction.DestinationAddress,
            DestinationTag = transaction.DestinationTag,
            SourceAddress = transaction.SourceAddress,
            State = TransactionState.SUBMITTED,
            NetworkFee = transaction.NetworkFee * 1.2m, // Increase fee by 20%
            Operation = transaction.Operation,
            FeeCurrency = transaction.FeeCurrency,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Hash = Guid.NewGuid().ToString(),
        };

        transaction.ReplacedByTxId = replacement.Id;

        _context.Transactions.Add(replacement);
        await _context.SaveChangesAsync();

        await NotifyTransactionsUpdatedAsync(transaction);

        _logger.LogInformation("Dropped transaction {TransactionId} and created replacement {ReplacementId}",
            txId, replacement.Id);

        return Ok(new DropTransactionResponseDto
        {
            Success = true,
            Transactions = new List<string> { TransactionCompositeId.Build(_workspace.WorkspaceId, replacement.Id) },
        });
    }

    [HttpPost("estimate_fee")]
    public async Task<ActionResult<EstimateFeeResponseDto>> EstimateFee([FromBody] EstimateFeeRequestDto request)
    {
        // Verify asset exists
        var asset = await _context.Assets.FindAsync(request.AssetId);
        if (asset == null)
        {
            throw FireblocksApiException.NotFound($"Asset {request.AssetId} not found");
        }

        // Use asset's configured fee (with Low/Medium/High multipliers)
        var baseFee = asset.BaseFee;
        var lowFee = baseFee.ToString(CultureInfo.InvariantCulture);
        var mediumFee = (baseFee * 1.5m).ToString(CultureInfo.InvariantCulture);
        var highFee = (baseFee * 2.5m).ToString(CultureInfo.InvariantCulture);

        // Calculate feePerByte (assuming typical transaction size of 250 bytes)
        const int estimatedTxSizeBytes = 250;
        var lowFeePerByte = (baseFee / estimatedTxSizeBytes).ToString(CultureInfo.InvariantCulture);
        var mediumFeePerByte = (baseFee * 1.5m / estimatedTxSizeBytes).ToString(CultureInfo.InvariantCulture);
        var highFeePerByte = (baseFee * 2.5m / estimatedTxSizeBytes).ToString(CultureInfo.InvariantCulture);

        // Calculate gasPrice (in Gwei for ETH-based assets)
        var lowGasPrice = baseFee.ToString(CultureInfo.InvariantCulture);
        var mediumGasPrice = (baseFee * 1.5m).ToString(CultureInfo.InvariantCulture);
        var highGasPrice = (baseFee * 2.5m).ToString(CultureInfo.InvariantCulture);

        var response = new EstimateFeeResponseDto
        {
            Low = new FeeEstimateDto
            {
                FeePerByte = lowFeePerByte,
                GasPrice = lowGasPrice,
                NetworkFee = lowFee,
                BaseFee = lowFee,
                PriorityFee = "1",
                GasLimit = "40000",
            },
            Medium = new FeeEstimateDto
            {
                FeePerByte = mediumFeePerByte,
                GasPrice = mediumGasPrice,
                NetworkFee = mediumFee,
                BaseFee = mediumFee,
                PriorityFee = "1",
                GasLimit = "40000",
            },
            High = new FeeEstimateDto
            {
                FeePerByte = highFeePerByte,
                GasPrice = highGasPrice,
                NetworkFee = highFee,
                BaseFee = highFee,
                PriorityFee = "1",
                GasLimit = "40000",
            },
        };

        return Ok(response);
    }

    [HttpGet("validate_address/{assetId}/{address}")]
    public async Task<ActionResult<ValidateAddressResponseDto>> ValidateAddress(string assetId, string address)
    {
        var asset = await _context.Assets.FindAsync(assetId);
        if (asset == null)
        {
            throw FireblocksApiException.NotFound($"Asset {assetId} not found");
        }

        bool isValid = ValidateAddressFormat(assetId, address);
        bool requiresTag = assetId == "XRP" || assetId == "XLM";

        var response = new ValidateAddressResponseDto
        {
            IsValid = isValid,
            IsActive = isValid,
            RequiresTag = requiresTag,
        };

        _logger.LogInformation("Validated address {Address} for asset {AssetId}: {IsValid}",
            address, assetId, isValid);

        return Ok(response);
    }





    private bool ValidateAddressFormat(string assetId, string address)
    {
        return assetId switch
        {
            "BTC" => address.StartsWith("bc1") || address.StartsWith("1") || address.StartsWith("3"),
            "ETH" or "USDT" or "USDC" => address.StartsWith("0x") && address.Length == 42,
            _ => !string.IsNullOrWhiteSpace(address),
        };
    }

    private Task<Transaction> FindTransactionByIdOrExternalIdAsync(string txId)
    {
        return RequireTransactionAsync(txId, allowExternalId: true);
    }

    private async Task<Transaction> RequireTransactionAsync(
        string txId,
        bool includeVaultAccount = false,
        bool allowExternalId = false)
    {
        try
        {
            return await _transactionIdResolver.RequireWorkspaceTransactionAsync(
                txId,
                includeVaultAccount: includeVaultAccount,
                allowExternalId: allowExternalId);
        }
        catch (KeyNotFoundException)
        {
            throw FireblocksApiException.NotFound($"Transaction {txId} not found");
        }
    }

    private async Task NotifyTransactionsUpdatedAsync(Transaction transaction)
    {
        var workspaceIds = await GetAffectedWorkspaceIdsAsync(transaction);
        foreach (var workspaceId in workspaceIds)
        {
            await _realtimeNotifier.NotifyTransactionsUpdatedAsync(workspaceId);
        }
    }

    private async Task<HashSet<string>> GetAffectedWorkspaceIdsAsync(Transaction transaction)
    {
        var workspaceIds = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(_workspace.WorkspaceId))
        {
            workspaceIds.Add(_workspace.WorkspaceId);
        }

        var addressValues = new[] { transaction.SourceAddress, transaction.DestinationAddress }
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address!)
            .Distinct()
            .ToList();

        if (addressValues.Count == 0)
        {
            return workspaceIds;
        }

        var addressWorkspaces = await _context.Addresses
            .Include(a => a.Wallet)
            .ThenInclude(w => w.VaultAccount)
            .Where(a => addressValues.Contains(a.AddressValue))
            .Select(a => a.Wallet.VaultAccount.WorkspaceId)
            .Distinct()
            .ToListAsync();

        foreach (var workspaceId in addressWorkspaces)
        {
            if (!string.IsNullOrWhiteSpace(workspaceId))
            {
                workspaceIds.Add(workspaceId);
            }
        }

        return workspaceIds;
    }

}


