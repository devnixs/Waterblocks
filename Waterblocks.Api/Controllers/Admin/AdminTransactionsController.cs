using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Hubs;
using Waterblocks.Api.Services;

namespace Waterblocks.Api.Controllers.Admin;

[ApiController]
[Route("admin/transactions")]
public class AdminTransactionsController : AdminControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<AdminTransactionsController> _logger;
    private readonly IHubContext<AdminHub> _hub;
    private readonly IBalanceService _balanceService;
    private readonly IAddressGenerator _addressGenerator;

    public AdminTransactionsController(
        FireblocksDbContext context,
        ILogger<AdminTransactionsController> logger,
        IHubContext<AdminHub> hub,
        WorkspaceContext workspace,
        IBalanceService balanceService,
        IAddressGenerator addressGenerator)
        : base(workspace)
    {
        _context = context;
        _logger = logger;
        _hub = hub;
        _balanceService = balanceService;
        _addressGenerator = addressGenerator;
    }

    [HttpGet]
    public async Task<ActionResult<AdminResponse<List<AdminTransactionDto>>>> GetTransactions()
    {
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<List<AdminTransactionDto>>();
        }

        var transactions = await _context.Transactions
            .Where(t => t.WorkspaceId == Workspace.WorkspaceId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var vaultNameLookup = await BuildVaultNameLookupAsync(transactions);
        var dtos = transactions.Select(t => MapToDto(t, vaultNameLookup)).ToList();
        return Ok(AdminResponse<List<AdminTransactionDto>>.Success(dtos));
    }

    [HttpGet("paged")]
    public async Task<ActionResult<AdminResponse<AdminTransactionsPageDto>>> GetTransactionsPaged(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? assetId = null,
        [FromQuery] string? transactionId = null,
        [FromQuery] string? hash = null)
    {
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<AdminTransactionsPageDto>();
        }

        var safePageIndex = Math.Max(0, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.Transactions
            .Where(t => t.WorkspaceId == Workspace.WorkspaceId);

        var normalizedAsset = assetId?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedAsset))
        {
            var assetLower = normalizedAsset.ToLowerInvariant();
            query = query.Where(t => t.AssetId.ToLower().Contains(assetLower));
        }

        var normalizedId = transactionId?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedId))
        {
            var idLower = normalizedId.ToLowerInvariant();
            query = query.Where(t => t.Id.ToLower().Contains(idLower));
        }

        var normalizedHash = hash?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedHash))
        {
            var hashLower = normalizedHash.ToLowerInvariant();
            query = query.Where(t => t.Hash != null && t.Hash.ToLower().Contains(hashLower));
        }

        var totalCount = await query.CountAsync();

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip(safePageIndex * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        var vaultNameLookup = await BuildVaultNameLookupAsync(transactions);
        var dtos = transactions.Select(t => MapToDto(t, vaultNameLookup)).ToList();

        var page = new AdminTransactionsPageDto
        {
            Items = dtos,
            TotalCount = totalCount,
            PageIndex = safePageIndex,
            PageSize = safePageSize,
        };

        return Ok(AdminResponse<AdminTransactionsPageDto>.Success(page));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AdminResponse<AdminTransactionDto>>> GetTransaction(string id)
    {
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<AdminTransactionDto>();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.WorkspaceId == Workspace.WorkspaceId);

        if (transaction == null)
        {
            return NotFound(AdminResponse<AdminTransactionDto>.Failure(
                $"Transaction {id} not found",
                "TRANSACTION_NOT_FOUND"));
        }

        var vaultNameLookup = await BuildVaultNameLookupAsync(transaction);
        return Ok(AdminResponse<AdminTransactionDto>.Success(MapToDto(transaction, vaultNameLookup)));
    }

    [HttpPost]
    public async Task<ActionResult<AdminResponse<AdminTransactionDto>>> CreateTransaction(
        [FromBody] CreateAdminTransactionRequestDto request)
    {
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<AdminTransactionDto>();
        }

        // Validate asset exists
        var asset = await _context.Assets.FindAsync(request.AssetId);
        if (asset == null)
        {
            return BadRequest(AdminResponse<AdminTransactionDto>.Failure(
                $"Asset {request.AssetId} not found",
                "ASSET_NOT_FOUND"));
        }

        if (!decimal.TryParse(request.Amount, out var amount) || amount <= 0)
        {
            return BadRequest(AdminResponse<AdminTransactionDto>.Failure(
                "Invalid amount",
                "INVALID_AMOUNT"));
        }

        var sourceType = NormalizeEndpointType(request.SourceType);
        var destinationType = NormalizeEndpointType(request.DestinationType);

        var sourceInternal = sourceType == "INTERNAL";
        var destinationInternal = destinationType == "INTERNAL";

        if (!sourceInternal && !destinationInternal)
        {
            return BadRequest(AdminResponse<AdminTransactionDto>.Failure(
                "At least one side of the transaction must be INTERNAL",
                "INVALID_TRANSACTION_SCOPE"));
        }

        var sourceVaultId = request.SourceVaultAccountId;
        var destinationVaultId = request.DestinationVaultAccountId;

        if (sourceInternal)
        {
            if (string.IsNullOrWhiteSpace(sourceVaultId))
            {
                return BadRequest(AdminResponse<AdminTransactionDto>.Failure(
                    "Source vault account is required for INTERNAL source",
                    "SOURCE_VAULT_REQUIRED"));
            }

            var sourceVault = await _context.VaultAccounts.FindAsync(sourceVaultId);
            if (sourceVault == null || sourceVault.WorkspaceId != Workspace.WorkspaceId)
            {
                return BadRequest(AdminResponse<AdminTransactionDto>.Failure(
                    $"Vault account {sourceVaultId} not found",
                    "VAULT_NOT_FOUND"));
            }
        }

        if (destinationInternal)
        {
            if (string.IsNullOrWhiteSpace(destinationVaultId))
            {
                return BadRequest(AdminResponse<AdminTransactionDto>.Failure(
                    "Destination vault account is required for INTERNAL destination",
                    "DESTINATION_VAULT_REQUIRED"));
            }

            var destinationVault = await _context.VaultAccounts.FindAsync(destinationVaultId);
            if (destinationVault == null || destinationVault.WorkspaceId != Workspace.WorkspaceId)
            {
                return BadRequest(AdminResponse<AdminTransactionDto>.Failure(
                    $"Vault account {destinationVaultId} not found",
                    "VAULT_NOT_FOUND"));
            }
        }

        var transactionVaultId = sourceInternal ? sourceVaultId : destinationVaultId;
        if (string.IsNullOrWhiteSpace(transactionVaultId))
        {
            return BadRequest(AdminResponse<AdminTransactionDto>.Failure(
                "Transaction requires an internal vault account",
                "VAULT_REQUIRED"));
        }

        var destinationAddress = request.DestinationAddress?.Trim() ?? "";
        if (destinationInternal)
        {
            var destinationWallet = await EnsureWalletWithDepositAddress(destinationVaultId!, request.AssetId);
            destinationAddress = destinationWallet.Addresses.FirstOrDefault()?.AddressValue ?? "";
        }
        else if (string.IsNullOrWhiteSpace(destinationAddress))
        {
            destinationAddress = _addressGenerator.GenerateExternalAddress(request.AssetId);
        }

        var sourceAddress = request.SourceAddress?.Trim();
        if (!sourceInternal && string.IsNullOrWhiteSpace(sourceAddress))
        {
            sourceAddress = _addressGenerator.GenerateExternalAddress(request.AssetId);
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            VaultAccountId = transactionVaultId!,
            WorkspaceId = Workspace.WorkspaceId,
            AssetId = request.AssetId,
            SourceType = sourceType,
            SourceAddress = sourceInternal ? null : sourceAddress,
            SourceVaultAccountId = sourceInternal ? sourceVaultId : null,
            DestinationType = destinationType,
            DestinationVaultAccountId = destinationInternal ? destinationVaultId : null,
            Amount = amount,
            DestinationAddress = destinationAddress,
            DestinationTag = request.DestinationTag,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        // Handle INCOMING vs OUTGOING
        var derivedType = sourceInternal && !destinationInternal
            ? "OUTGOING"
            : !sourceInternal && destinationInternal
                ? "INCOMING"
                : "TRANSFER";

        if (derivedType == "INCOMING")
        {
            // Incoming transactions are automatically completed and update balance
            transaction.State = ResolveInitialState(request.InitialState, TransactionState.COMPLETED);
            if (transaction.State == TransactionState.COMPLETED)
            {
                transaction.Confirmations = 6;
                // Credit incoming funds to destination wallet
                await _balanceService.CreditIncomingAsync(transaction);
            }

            _logger.LogInformation("Created INCOMING transaction {TxId} for {Amount} {AssetId}",
                transaction.Id, amount, request.AssetId);
        }
        else
        {
            // OUTGOING or TRANSFER transactions - validate and reserve funds
            var reserveResult = await _balanceService.ReserveFundsAsync(transaction);
            if (!reserveResult.Success)
            {
                return BadRequest(AdminResponse<AdminTransactionDto>.Failure(
                    reserveResult.ErrorMessage!,
                    reserveResult.ErrorCode!));
            }

            // OUTGOING transactions start in specified state or SUBMITTED
            transaction.State = ResolveInitialState(request.InitialState, TransactionState.SUBMITTED);

            _logger.LogInformation("Created OUTGOING transaction {TxId} in state {State}",
                transaction.Id, transaction.State);
        }

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        var vaultNameLookup = await BuildVaultNameLookupAsync(transaction);
        var dto = MapToDto(transaction, vaultNameLookup);
        await _hub.Clients.Group(Workspace.WorkspaceId).SendAsync("transactionUpserted", dto);
        await _hub.Clients.Group(Workspace.WorkspaceId).SendAsync("transactionsUpdated");

        return Ok(AdminResponse<AdminTransactionDto>.Success(dto));
    }

    // Positive State Transitions
    [HttpPost("{id}/approve")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> ApproveTransaction(string id)
    {
        return await TransitionTransaction(id, TransactionState.PENDING_AUTHORIZATION);
    }

    [HttpPost("{id}/sign")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> SignTransaction(string id)
    {
        return await TransitionTransaction(id, TransactionState.QUEUED);
    }

    [HttpPost("{id}/broadcast")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> BroadcastTransaction(string id)
    {
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<TransactionStateDto>();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.WorkspaceId == Workspace.WorkspaceId);
        if (transaction == null)
        {
            return NotFound(AdminResponse<TransactionStateDto>.Failure(
                $"Transaction {id} not found",
                "TRANSACTION_NOT_FOUND"));
        }

        // Generate a mock transaction hash when broadcasting
        if (string.IsNullOrEmpty(transaction.Hash))
        {
            transaction.Hash = $"0x{Guid.NewGuid():N}";
        }

        return await TransitionTransaction(id, TransactionState.BROADCASTING);
    }

    [HttpPost("{id}/confirm")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> ConfirmTransaction(string id)
    {
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<TransactionStateDto>();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.WorkspaceId == Workspace.WorkspaceId);
        if (transaction == null)
        {
            return NotFound(AdminResponse<TransactionStateDto>.Failure(
                $"Transaction {id} not found",
                "TRANSACTION_NOT_FOUND"));
        }

        // Increment confirmations
        transaction.Confirmations++;

        return await TransitionTransaction(id, TransactionState.CONFIRMING);
    }

    [HttpPost("{id}/complete")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> CompleteTransaction(string id)
    {
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<TransactionStateDto>();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.WorkspaceId == Workspace.WorkspaceId);
        if (transaction == null)
        {
            return NotFound(AdminResponse<TransactionStateDto>.Failure(
                $"Transaction {id} not found",
                "TRANSACTION_NOT_FOUND"));
        }

        // Set final confirmations
        if (transaction.Confirmations == 0)
        {
            transaction.Confirmations = 6;
        }

        // Update balances: source -amount, destination +amount
        await _balanceService.CompleteTransactionAsync(transaction);

        return await TransitionTransaction(id, TransactionState.COMPLETED);
    }

    // Failure Simulation Endpoints
    [HttpPost("{id}/fail")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> FailTransaction(
        string id,
        [FromBody] FailTransactionRequestDto? request = null)
    {
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<TransactionStateDto>();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.WorkspaceId == Workspace.WorkspaceId);
        if (transaction == null)
        {
            return NotFound(AdminResponse<TransactionStateDto>.Failure(
                $"Transaction {id} not found",
                "TRANSACTION_NOT_FOUND"));
        }

        if (transaction.State.IsTerminal())
        {
            return BadRequest(AdminResponse<TransactionStateDto>.Failure(
                $"Cannot fail transaction in terminal state {transaction.State}",
                "INVALID_STATE_TRANSITION"));
        }

        // Rollback reserved pending funds
        await _balanceService.RollbackTransactionAsync(transaction);

        transaction.State = TransactionState.FAILED;
        transaction.FailureReason = request?.Reason ?? "NETWORK_ERROR";
        transaction.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        var vaultNameLookup = await BuildVaultNameLookupAsync(transaction);
        await _hub.Clients.Group(Workspace.WorkspaceId!).SendAsync("transactionUpserted", MapToDto(transaction, vaultNameLookup));
        await _hub.Clients.Group(Workspace.WorkspaceId!).SendAsync("transactionsUpdated");

        _logger.LogInformation("Failed transaction {TxId} with reason {Reason}",
            id, transaction.FailureReason);

        var result = new TransactionStateDto
        {
            Id = transaction.Id,
            State = transaction.State.ToString(),
        };

        return Ok(AdminResponse<TransactionStateDto>.Success(result));
    }

    [HttpPost("{id}/reject")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> RejectTransaction(string id)
    {
        return await TransitionTransaction(id, TransactionState.REJECTED);
    }

    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> CancelTransaction(string id)
    {
        return await TransitionTransaction(id, TransactionState.CANCELLED);
    }

    [HttpPost("{id}/timeout")]
    public async Task<ActionResult<AdminResponse<TransactionStateDto>>> TimeoutTransaction(string id)
    {
        return await TransitionTransaction(id, TransactionState.TIMEOUT);
    }

    private async Task<ActionResult<AdminResponse<TransactionStateDto>>> TransitionTransaction(
        string id,
        TransactionState newState)
    {
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<TransactionStateDto>();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.WorkspaceId == Workspace.WorkspaceId);
        if (transaction == null)
        {
            return NotFound(AdminResponse<TransactionStateDto>.Failure(
                $"Transaction {id} not found",
                "TRANSACTION_NOT_FOUND"));
        }

        // Check if already in the target state (idempotent)
        if (transaction.State == newState)
        {
            _logger.LogInformation("Transaction {TxId} already in state {State}",
                id, newState);

            var existingResult = new TransactionStateDto
            {
                Id = transaction.Id,
                State = transaction.State.ToString(),
            };

            return Ok(AdminResponse<TransactionStateDto>.Success(existingResult));
        }

        // Validate transition
        if (!transaction.State.CanTransitionTo(newState))
        {
            return BadRequest(AdminResponse<TransactionStateDto>.Failure(
                $"Invalid transition from {transaction.State} to {newState}",
                "INVALID_STATE_TRANSITION"));
        }

        // Handle balance updates based on target state
        if (newState == TransactionState.REJECTED ||
            newState == TransactionState.CANCELLED ||
            newState == TransactionState.TIMEOUT)
        {
            // Rollback reserved pending funds for failure states
            await _balanceService.RollbackTransactionAsync(transaction);
        }

        transaction.TransitionTo(newState);
        await _context.SaveChangesAsync();
        var vaultNameLookup = await BuildVaultNameLookupAsync(transaction);
        await _hub.Clients.Group(Workspace.WorkspaceId!).SendAsync("transactionUpserted", MapToDto(transaction, vaultNameLookup));
        await _hub.Clients.Group(Workspace.WorkspaceId!).SendAsync("transactionsUpdated");

        _logger.LogInformation("Transitioned transaction {TxId} from {OldState} to {NewState}",
            id, transaction.State, newState);

        var result = new TransactionStateDto
        {
            Id = transaction.Id,
            State = transaction.State.ToString(),
        };

        return Ok(AdminResponse<TransactionStateDto>.Success(result));
    }

    private AdminTransactionDto MapToDto(Transaction transaction, IReadOnlyDictionary<string, string> vaultNameLookup)
    {
        return new AdminTransactionDto
        {
            Id = transaction.Id,
            VaultAccountId = transaction.VaultAccountId,
            AssetId = transaction.AssetId,
            SourceType = transaction.SourceType,
            SourceAddress = transaction.SourceAddress,
            SourceVaultAccountId = transaction.SourceVaultAccountId,
            SourceVaultAccountName = ResolveVaultName(vaultNameLookup, transaction.SourceVaultAccountId),
            DestinationType = transaction.DestinationType,
            DestinationVaultAccountId = transaction.DestinationVaultAccountId,
            DestinationVaultAccountName = ResolveVaultName(vaultNameLookup, transaction.DestinationVaultAccountId),
            Amount = transaction.Amount.ToString("F18"),
            DestinationAddress = transaction.DestinationAddress,
            DestinationTag = transaction.DestinationTag,
            State = transaction.State.ToString(),
            Hash = transaction.Hash,
            Fee = transaction.Fee.ToString("F18"),
            NetworkFee = transaction.NetworkFee.ToString("F18"),
            IsFrozen = transaction.IsFrozen,
            FailureReason = transaction.FailureReason,
            ReplacedByTxId = transaction.ReplacedByTxId,
            Confirmations = transaction.Confirmations,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt,
        };
    }

    private async Task<Dictionary<string, string>> BuildVaultNameLookupAsync(IEnumerable<Transaction> transactions)
    {
        var vaultIds = transactions
            .SelectMany(t => new[] { t.VaultAccountId, t.SourceVaultAccountId, t.DestinationVaultAccountId })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        if (vaultIds.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        return await _context.VaultAccounts
            .Where(v => vaultIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Name);
    }

    private Task<Dictionary<string, string>> BuildVaultNameLookupAsync(Transaction transaction)
    {
        return BuildVaultNameLookupAsync(new[] { transaction });
    }

    private static string? ResolveVaultName(IReadOnlyDictionary<string, string> vaultNameLookup, string? vaultId)
    {
        if (string.IsNullOrWhiteSpace(vaultId))
        {
            return null;
        }

        return vaultNameLookup.TryGetValue(vaultId, out var name) ? name : null;
    }

    private static string NormalizeEndpointType(string? type)
    {
        var normalized = (type ?? "EXTERNAL").Trim().ToUpperInvariant();
        return normalized == "INTERNAL" ? "INTERNAL" : "EXTERNAL";
    }

    private static TransactionState ResolveInitialState(string? initialState, TransactionState fallback)
    {
        if (!string.IsNullOrEmpty(initialState) &&
            Enum.TryParse<TransactionState>(initialState, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private async Task<Wallet> EnsureWalletWithDepositAddress(string vaultAccountId, string assetId)
    {
        var wallet = await _context.Wallets
            .Include(w => w.Addresses)
            .Include(w => w.VaultAccount)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId && w.VaultAccount.WorkspaceId == Workspace.WorkspaceId);

        if (wallet == null)
        {
            var vault = await _context.VaultAccounts
                .FirstOrDefaultAsync(v => v.Id == vaultAccountId && v.WorkspaceId == Workspace.WorkspaceId);
            if (vault == null)
            {
                throw new KeyNotFoundException($"Vault account {vaultAccountId} not found");
            }

            wallet = new Wallet
            {
                VaultAccountId = vaultAccountId,
                AssetId = assetId,
                Balance = 0,
                LockedAmount = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();
        }

        if (!wallet.Addresses.Any())
        {
            var address = new Address
            {
                AddressValue = _addressGenerator.GenerateAdminDepositAddress(assetId, vaultAccountId),
                Type = "Permanent",
                WalletId = wallet.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _context.Addresses.Add(address);
            await _context.SaveChangesAsync();
            wallet.Addresses.Add(address);
        }

        return wallet;
    }

}
