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

        var addressLookup = await BuildAddressOwnershipLookupAsync(transactions);
        var dtos = transactions.Select(t => MapToDto(t, addressLookup)).ToList();
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

        var addressLookup = await BuildAddressOwnershipLookupAsync(new[] { transaction });
        return Ok(MapToDto(transaction, addressLookup));
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

        var addressLookup = await BuildAddressOwnershipLookupAsync(new[] { transaction });
        return Ok(MapToDto(transaction, addressLookup));
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

    private TransactionDto MapToDto(Transaction transaction, IReadOnlyDictionary<string, AddressOwnership> addressLookup)
    {
        var createdAtUnix = (decimal)transaction.CreatedAt.ToUnixTimeMilliseconds();
        var lastUpdatedUnix = (decimal)transaction.UpdatedAt.ToUnixTimeMilliseconds();
        var amountStr = transaction.Amount.ToString(CultureInfo.InvariantCulture);
        var networkFeeStr = transaction.NetworkFee.ToString(CultureInfo.InvariantCulture);
        var serviceFeeStr = transaction.ServiceFee.ToString(CultureInfo.InvariantCulture);
        var sourceOwnership = ResolveAddressOwnership(addressLookup, transaction.AssetId, transaction.SourceAddress);
        var destinationOwnership = ResolveAddressOwnership(addressLookup, transaction.AssetId, transaction.DestinationAddress);
        var sourceType = sourceOwnership != null ? "VAULT_ACCOUNT" : "ONE_TIME_ADDRESS";
        var destinationType = destinationOwnership != null ? "VAULT_ACCOUNT" : "ONE_TIME_ADDRESS";

        return new TransactionDto
        {
            Id = transaction.Id,
            AssetId = transaction.AssetId,
            Source = new TransferPeerPathResponseDto
            {
                Type = sourceType,
                Id = sourceOwnership?.VaultAccountId ?? string.Empty,
                Name = sourceOwnership?.VaultAccountName ?? string.Empty,
                SubType = "DEFAULT",
                VirtualType = "UNKNOWN",
                VirtualId = string.Empty,
            },
            Destination = new TransferPeerPathResponseDto
            {
                Type = destinationType,
                Id = destinationOwnership?.VaultAccountId ?? string.Empty,
                Name = destinationOwnership?.VaultAccountName ?? string.Empty,
                SubType = "DEFAULT",
                VirtualType = "UNKNOWN",
                VirtualId = string.Empty,
            },
            RequestedAmount = transaction.RequestedAmount.ToString(CultureInfo.InvariantCulture),
            Amount = amountStr,
            NetAmount = (transaction.Amount - transaction.NetworkFee - transaction.ServiceFee).ToString(CultureInfo.InvariantCulture),
            AmountUSD = null, // USD conversion not implemented
            ServiceFee = serviceFeeStr,
            NetworkFee = networkFeeStr,
            CreatedAt = createdAtUnix,
            LastUpdated = lastUpdatedUnix,
            Status = transaction.State.ToString(),
            TxHash = transaction.Hash ?? string.Empty,
            Tag = transaction.DestinationTag ?? string.Empty,
            SubStatus = transaction.SubStatus,
            DestinationAddress = transaction.DestinationAddress ?? string.Empty,
            SourceAddress = transaction.SourceAddress ?? string.Empty,
            DestinationAddressDescription = string.Empty,
            DestinationTag = transaction.DestinationTag ?? string.Empty,
            SignedBy = new List<string>(),
            CreatedBy = string.Empty,
            RejectedBy = string.Empty,
            AddressType = "PERMANENT",
            Note = transaction.Note ?? string.Empty,
            ExchangeTxId = string.Empty,
            FeeCurrency = transaction.FeeCurrency ?? transaction.AssetId ?? string.Empty,
            Operation = transaction.Operation ?? "TRANSFER",
            NetworkRecords = new List<NetworkRecordDto>(),
            AmlScreeningResult = new AmlScreeningResultDto
            {
                Provider = string.Empty,
                Payload = new Dictionary<string, object>(),
            },
            CustomerRefId = transaction.CustomerRefId ?? string.Empty,
            NumOfConfirmations = transaction.Confirmations,
            SignedMessages = new List<SignedMessageDto>(),
            ExtraParameters = new Dictionary<string, object>(),
            ExternalTxId = transaction.ExternalTxId ?? string.Empty,
            ReplacedTxHash = transaction.ReplacedByTxId != null ? transaction.Hash ?? string.Empty : string.Empty,
            Destinations = new List<TransactionResponseDestinationDto>(),
            BlockInfo = new BlockInfoDto
            {
                BlockHeight = "100",
                BlockHash = "xxxyyy",
            },
            AuthorizationInfo = new AuthorizationInfoDto
            {
                AllowOperatorAsAuthorizer = false,
                Logic = "AND",
                Groups = new List<AuthorizationGroupDto>(),
            },
            AmountInfo = new AmountInfoDto
            {
                Amount = amountStr,
                RequestedAmount = transaction.RequestedAmount.ToString(CultureInfo.InvariantCulture),
                NetAmount = (transaction.Amount - transaction.NetworkFee - transaction.ServiceFee).ToString(CultureInfo.InvariantCulture),
                AmountUSD = string.Empty,
            },
            Index = null,
            BlockchainIndex = string.Empty,
        };
    }

    private sealed record AddressOwnership(string VaultAccountId, string VaultAccountName);

    private static string BuildAddressKey(string assetId, string address)
    {
        return $"{assetId}|{address}";
    }

    private static AddressOwnership? ResolveAddressOwnership(
        IReadOnlyDictionary<string, AddressOwnership> lookup,
        string assetId,
        string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        return lookup.TryGetValue(BuildAddressKey(assetId, address), out var ownership)
            ? ownership
            : null;
    }

    private async Task<Dictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(IEnumerable<Transaction> transactions)
    {
        var addressValues = transactions
            .SelectMany(t => new[] { t.SourceAddress, t.DestinationAddress })
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address!)
            .Distinct()
            .ToList();

        if (addressValues.Count == 0)
        {
            return new Dictionary<string, AddressOwnership>();
        }

        var addresses = await _context.Addresses
            .Include(a => a.Wallet)
            .ThenInclude(w => w.VaultAccount)
            .Where(a => addressValues.Contains(a.AddressValue) && a.Wallet.VaultAccount.WorkspaceId == _workspace.WorkspaceId)
            .ToListAsync();

        var lookup = new Dictionary<string, AddressOwnership>();
        foreach (var address in addresses)
        {
            var wallet = address.Wallet;
            var vault = wallet?.VaultAccount;
            if (wallet == null || vault == null)
            {
                continue;
            }

            var key = BuildAddressKey(wallet.AssetId, address.AddressValue);
            if (!lookup.ContainsKey(key))
            {
                lookup[key] = new AddressOwnership(vault.Id, vault.Name);
            }
        }

        return lookup;
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
