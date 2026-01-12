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

    public TransactionsController(
        FireblocksDbContext context,
        ILogger<TransactionsController> logger,
        WorkspaceContext workspace,
        ITransactionService transactionService)
    {
        _context = context;
        _logger = logger;
        _workspace = workspace;
        _transactionService = transactionService;
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
            throw new UnauthorizedAccessException("Workspace is required");
        }

        var query = _context.Transactions
            .Where(t => t.WorkspaceId == _workspace.WorkspaceId)
            .Include(t => t.VaultAccount)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<TransactionState>(status, out var stateFilter))
        {
            query = query.Where(t => t.State == stateFilter);
        }

        if (!string.IsNullOrWhiteSpace(before))
        {
            if (!long.TryParse(before, out var beforeMs))
            {
                return BadRequest($"Invalid before parameter: {before}");
            }

            var beforeDate = DateTimeOffset.FromUnixTimeMilliseconds(beforeMs);
            query = query.Where(t => t.CreatedAt < beforeDate);
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            if (!long.TryParse(after, out var afterMs))
            {
                return BadRequest($"Invalid after parameter: {after}");
            }

            var afterDate = DateTimeOffset.FromUnixTimeMilliseconds(afterMs);
            query = query.Where(t => t.CreatedAt > afterDate);
        }

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var dtos = transactions.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    [HttpGet("{txId}")]
    public async Task<ActionResult<TransactionDto>> GetTransaction(string txId)
    {
        var transaction = await _context.Transactions
            .Include(t => t.VaultAccount)
            .FirstOrDefaultAsync(t => t.Id == txId && t.WorkspaceId == _workspace.WorkspaceId);

        if (transaction == null)
        {
            throw new KeyNotFoundException($"Transaction {txId} not found");
        }

        return Ok(MapToDto(transaction));
    }

    [HttpGet("external_tx_id/{externalTxId}")]
    public async Task<ActionResult<TransactionDto>> GetTransactionByExternalId(string externalTxId)
    {
        var transaction = await _context.Transactions
            .Include(t => t.VaultAccount)
            .FirstOrDefaultAsync(t => t.ExternalTxId == externalTxId && t.WorkspaceId == _workspace.WorkspaceId);

        if (transaction == null)
        {
            throw new KeyNotFoundException($"Transaction with external ID {externalTxId} not found");
        }

        return Ok(MapToDto(transaction));
    }

    [HttpPost("{txId}/cancel")]
    public async Task<ActionResult<CancelTransactionResponseDto>> CancelTransaction(string txId)
    {
        var transaction = await FindTransactionByIdOrExternalIdAsync(txId);

        if (transaction.State.IsTerminal())
        {
            throw new InvalidOperationException($"Cannot cancel transaction in {transaction.State} state");
        }

        transaction.TransitionTo(TransactionState.CANCELLED);
        transaction.SubStatus = "CANCELLED_BY_USER";
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cancelled transaction {TransactionId}", txId);

        return Ok(new CancelTransactionResponseDto { Success = true });
    }

    [HttpPost("{txId}/freeze")]
    public async Task<ActionResult<FreezeTransactionResponseDto>> FreezeTransaction(string txId)
    {
        var transaction = await FindTransactionByIdOrExternalIdAsync(txId);

        transaction.Freeze();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Froze transaction {TransactionId}", txId);

        return Ok(new FreezeTransactionResponseDto { Success = true });
    }

    [HttpPost("{txId}/unfreeze")]
    public async Task<ActionResult<FreezeTransactionResponseDto>> UnfreezeTransaction(string txId)
    {
        var transaction = await FindTransactionByIdOrExternalIdAsync(txId);

        transaction.Unfreeze();
        await _context.SaveChangesAsync();

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
            throw new InvalidOperationException("Drop operation only supported for ETH transactions");
        }

        if (transaction.State.IsTerminal())
        {
            throw new InvalidOperationException($"Cannot drop transaction in {transaction.State} state");
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
            DestinationType = transaction.DestinationType,
            State = TransactionState.SUBMITTED,
            NetworkFee = transaction.NetworkFee * 1.2m, // Increase fee by 20%
            Operation = transaction.Operation,
            FeeCurrency = transaction.FeeCurrency,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        transaction.ReplacedByTxId = replacement.Id;

        _context.Transactions.Add(replacement);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Dropped transaction {TransactionId} and created replacement {ReplacementId}",
            txId, replacement.Id);

        return Ok(new DropTransactionResponseDto
        {
            Success = true,
            Transactions = new List<string> { replacement.Id },
        });
    }

    [HttpPost("estimate_fee")]
    public async Task<ActionResult<EstimateFeeResponseDto>> EstimateFee([FromBody] EstimateFeeRequestDto request)
    {
        // Verify asset exists
        var asset = await _context.Assets.FindAsync(request.AssetId);
        if (asset == null)
        {
            throw new KeyNotFoundException($"Asset {request.AssetId} not found");
        }

        // Use asset's configured fee (with Low/Medium/High multipliers)
        var baseFee = asset.BaseFee;
        var lowFee = baseFee.ToString("G29");
        var mediumFee = (baseFee * 1.5m).ToString("G29");
        var highFee = (baseFee * 2.5m).ToString("G29");

        var response = new EstimateFeeResponseDto
        {
            Low = new FeeEstimateDto
            {
                NetworkFee = lowFee,
                BaseFee = lowFee,
            },
            Medium = new FeeEstimateDto
            {
                NetworkFee = mediumFee,
                BaseFee = mediumFee,
            },
            High = new FeeEstimateDto
            {
                NetworkFee = highFee,
                BaseFee = highFee,
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
            throw new KeyNotFoundException($"Asset {assetId} not found");
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

    private TransactionDto MapToDto(Transaction transaction)
    {
        var createdAtUnix = (decimal)transaction.CreatedAt.ToUnixTimeMilliseconds();
        var lastUpdatedUnix = (decimal)transaction.UpdatedAt.ToUnixTimeMilliseconds();
        var amountStr = transaction.Amount.ToString("G29");
        var networkFeeStr = transaction.NetworkFee.ToString("G29");
        var serviceFeeStr = transaction.ServiceFee.ToString("G29");

        return new TransactionDto
        {
            Id = transaction.Id,
            AssetId = transaction.AssetId,
            Source = new TransferPeerPathResponseDto
            {
                Type = transaction.SourceType,
                Id = transaction.VaultAccountId,
                Name = transaction.VaultAccount?.Name,
            },
            Destination = new TransferPeerPathResponseDto
            {
                Type = transaction.DestinationType,
                Id = transaction.DestinationVaultAccountId,
            },
            RequestedAmount = transaction.RequestedAmount.ToString("G29"),
            Amount = amountStr,
            NetAmount = (transaction.Amount - transaction.NetworkFee - transaction.ServiceFee).ToString("G29"),
            AmountUSD = null, // USD conversion not implemented
            ServiceFee = serviceFeeStr,
            NetworkFee = networkFeeStr,
            CreatedAt = createdAtUnix,
            LastUpdated = lastUpdatedUnix,
            Status = transaction.State.ToString(),
            TxHash = transaction.Hash,
            Tag = transaction.DestinationTag,
            SubStatus = transaction.SubStatus,
            DestinationAddress = transaction.DestinationAddress,
            SourceAddress = transaction.SourceAddress,
            DestinationAddressDescription = null,
            DestinationTag = transaction.DestinationTag,
            SignedBy = new List<string>(),
            CreatedBy = null,
            RejectedBy = null,
            AddressType = "PERMANENT",
            Note = transaction.Note,
            ExchangeTxId = null,
            FeeCurrency = transaction.FeeCurrency ?? transaction.AssetId,
            Operation = transaction.Operation,
            NetworkRecords = null,
            AmlScreeningResult = null,
            CustomerRefId = transaction.CustomerRefId,
            NumOfConfirmations = transaction.Confirmations,
            SignedMessages = null,
            ExtraParameters = null,
            ExternalTxId = transaction.ExternalTxId,
            ReplacedTxHash = transaction.ReplacedByTxId != null ? transaction.Hash : null,
            Destinations = null,
            BlockInfo = new BlockInfoDto
            {
                BlockHeight = null,
                BlockHash = null,
            },
            AuthorizationInfo = null,
            AmountInfo = new AmountInfoDto
            {
                Amount = amountStr,
                RequestedAmount = transaction.RequestedAmount.ToString("G29"),
                NetAmount = (transaction.Amount - transaction.NetworkFee - transaction.ServiceFee).ToString("G29"),
                AmountUSD = null,
            },
            Index = null,
            BlockchainIndex = null,
        };
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

    private async Task<Transaction> FindTransactionByIdOrExternalIdAsync(string txId)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t =>
                t.WorkspaceId == _workspace.WorkspaceId
                && (t.Id == txId || t.ExternalTxId == txId));

        if (transaction == null)
        {
            throw new KeyNotFoundException($"Transaction {txId} not found");
        }

        return transaction;
    }
}
