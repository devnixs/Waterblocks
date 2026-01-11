using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FireblocksReplacement.Api.Infrastructure.Db;
using FireblocksReplacement.Api.Models;
using FireblocksReplacement.Api.Dtos.Fireblocks;

namespace FireblocksReplacement.Api.Controllers;

[ApiController]
[Route("transactions")]
public class TransactionsController : ControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(FireblocksDbContext context, ILogger<TransactionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> CreateTransaction([FromBody] CreateTransactionRequestDto request)
    {
        // Validate source vault exists
        var vaultAccountId = request.Source.Id;
        var vaultAccount = await _context.VaultAccounts.FindAsync(vaultAccountId);
        if (vaultAccount == null)
        {
            throw new KeyNotFoundException($"Vault account {vaultAccountId} not found");
        }

        // Validate asset exists
        var asset = await _context.Assets.FindAsync(request.AssetId);
        if (asset == null)
        {
            throw new KeyNotFoundException($"Asset {request.AssetId} not found");
        }

        // Validate destination address
        var destinationAddress = request.Destination.OneTimeAddress?.Address ?? string.Empty;
        if (!ValidateAddressFormat(request.AssetId, destinationAddress))
        {
            throw new ArgumentException($"Invalid destination address format for asset {request.AssetId}");
        }

        // Parse amount
        if (!decimal.TryParse(request.Amount, out var amount) || amount <= 0)
        {
            throw new ArgumentException("Invalid amount");
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            VaultAccountId = vaultAccountId,
            AssetId = request.AssetId,
            Amount = amount,
            DestinationAddress = destinationAddress,
            DestinationTag = request.Destination.OneTimeAddress?.Tag,
            State = TransactionState.SUBMITTED,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created transaction {TransactionId} for {Amount} {AssetId} from vault {VaultAccountId}",
            transaction.Id, amount, request.AssetId, vaultAccountId);

        return Ok(MapToDto(transaction));
    }

    [HttpGet]
    public async Task<ActionResult<List<TransactionDto>>> GetTransactions(
        [FromQuery] string? status = null,
        [FromQuery] int limit = 100)
    {
        var query = _context.Transactions.AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<TransactionState>(status, out var stateFilter))
        {
            query = query.Where(t => t.State == stateFilter);
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
        var transaction = await _context.Transactions.FindAsync(txId);

        if (transaction == null)
        {
            throw new KeyNotFoundException($"Transaction {txId} not found");
        }

        return Ok(MapToDto(transaction));
    }

    [HttpPost("{txId}/cancel")]
    public async Task<ActionResult<TransactionDto>> CancelTransaction(string txId)
    {
        var transaction = await _context.Transactions.FindAsync(txId);

        if (transaction == null)
        {
            throw new KeyNotFoundException($"Transaction {txId} not found");
        }

        if (transaction.State.IsTerminal())
        {
            throw new InvalidOperationException($"Cannot cancel transaction in {transaction.State} state");
        }

        transaction.TransitionTo(TransactionState.CANCELLED);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cancelled transaction {TransactionId}", txId);

        return Ok(MapToDto(transaction));
    }

    [HttpPost("{txId}/freeze")]
    public async Task<ActionResult<TransactionDto>> FreezeTransaction(string txId)
    {
        var transaction = await _context.Transactions.FindAsync(txId);

        if (transaction == null)
        {
            throw new KeyNotFoundException($"Transaction {txId} not found");
        }

        transaction.Freeze();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Froze transaction {TransactionId}", txId);

        return Ok(MapToDto(transaction));
    }

    [HttpPost("{txId}/unfreeze")]
    public async Task<ActionResult<TransactionDto>> UnfreezeTransaction(string txId)
    {
        var transaction = await _context.Transactions.FindAsync(txId);

        if (transaction == null)
        {
            throw new KeyNotFoundException($"Transaction {txId} not found");
        }

        transaction.Unfreeze();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Unfroze transaction {TransactionId}", txId);

        return Ok(MapToDto(transaction));
    }

    [HttpPost("{txId}/drop")]
    public async Task<ActionResult<object>> DropTransaction(string txId, [FromBody] CreateTransactionRequestDto? replacementRequest = null)
    {
        var transaction = await _context.Transactions.FindAsync(txId);

        if (transaction == null)
        {
            throw new KeyNotFoundException($"Transaction {txId} not found");
        }

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
        transaction.FailureReason = "Replaced by higher fee transaction";

        // Create replacement transaction with higher fee
        var replacement = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            VaultAccountId = transaction.VaultAccountId,
            AssetId = transaction.AssetId,
            Amount = transaction.Amount,
            DestinationAddress = transaction.DestinationAddress,
            DestinationTag = transaction.DestinationTag,
            State = TransactionState.SUBMITTED,
            NetworkFee = transaction.NetworkFee * 1.2m, // Increase fee by 20%
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        transaction.ReplacedByTxId = replacement.Id;

        _context.Transactions.Add(replacement);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Dropped transaction {TransactionId} and created replacement {ReplacementId}",
            txId, replacement.Id);

        var response = new
        {
            original = MapToDto(transaction),
            replacement = MapToDto(replacement)
        };

        return Ok(response);
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

        // Mock fee estimation
        var baseFee = GetBaseFee(request.AssetId);

        var response = new EstimateFeeResponseDto
        {
            Low = new FeeEstimateDto { Fee = (baseFee * 0.8m).ToString("F8"), GasPrice = "20" },
            Medium = new FeeEstimateDto { Fee = baseFee.ToString("F8"), GasPrice = "30" },
            High = new FeeEstimateDto { Fee = (baseFee * 1.5m).ToString("F8"), GasPrice = "50" }
        };

        return Ok(response);
    }

    [HttpGet("validate_address/{assetId}/{address}")]
    public async Task<ActionResult<object>> ValidateAddress(string assetId, string address)
    {
        var asset = await _context.Assets.FindAsync(assetId);
        if (asset == null)
        {
            throw new KeyNotFoundException($"Asset {assetId} not found");
        }

        bool isValid = ValidateAddressFormat(assetId, address);

        var response = new
        {
            isValid,
            addressType = isValid ? "PERMANENT" : (string?)null
        };

        _logger.LogInformation("Validated address {Address} for asset {AssetId}: {IsValid}",
            address, assetId, isValid);

        return Ok(response);
    }

    private TransactionDto MapToDto(Transaction transaction)
    {
        return new TransactionDto
        {
            Id = transaction.Id,
            AssetId = transaction.AssetId,
            Source = new SourceDto
            {
                Type = "VAULT_ACCOUNT",
                Id = transaction.VaultAccountId
            },
            Destination = new DestinationDto
            {
                Type = "ONE_TIME_ADDRESS",
                OneTimeAddress = new OneTimeAddressDto
                {
                    Address = transaction.DestinationAddress,
                    Tag = transaction.DestinationTag
                }
            },
            Amount = transaction.Amount.ToString("F18"),
            Fee = transaction.Fee.ToString("F18"),
            NetworkFee = transaction.NetworkFee.ToString("F18"),
            Status = transaction.State.ToString(),
            TxHash = transaction.Hash,
            CreatedAt = new DateTimeOffset(transaction.CreatedAt).ToUnixTimeMilliseconds(),
            LastUpdated = new DateTimeOffset(transaction.UpdatedAt).ToUnixTimeMilliseconds(),
            NumOfConfirmations = transaction.Confirmations
        };
    }

    private bool ValidateAddressFormat(string assetId, string address)
    {
        return assetId switch
        {
            "BTC" => address.StartsWith("bc1") || address.StartsWith("1") || address.StartsWith("3"),
            "ETH" or "USDT" or "USDC" => address.StartsWith("0x") && address.Length == 42,
            _ => !string.IsNullOrWhiteSpace(address)
        };
    }

    private decimal GetBaseFee(string assetId)
    {
        return assetId switch
        {
            "BTC" => 0.0001m,
            "ETH" => 0.001m,
            "USDT" => 0.0005m,
            "USDC" => 0.0005m,
            _ => 0.0001m
        };
    }
}
