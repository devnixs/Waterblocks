using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Hubs;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public interface IAdminTransactionService
{
    Task<AdminServiceResult<List<AdminTransactionDto>>> GetTransactionsAsync();
    Task<AdminServiceResult<AdminTransactionsPageDto>> GetTransactionsPagedAsync(int pageIndex, int pageSize, string? assetId, string? transactionId, string? hash);
    Task<AdminServiceResult<AdminTransactionDto>> GetTransactionAsync(string id);
    Task<AdminServiceResult<AdminTransactionDto>> CreateTransactionAsync(CreateAdminTransactionRequestDto request);
    Task<AdminServiceResult<TransactionStateDto>> ApproveAsync(string id);
    Task<AdminServiceResult<TransactionStateDto>> SignAsync(string id);
    Task<AdminServiceResult<TransactionStateDto>> BroadcastAsync(string id);
    Task<AdminServiceResult<TransactionStateDto>> ConfirmAsync(string id);
    Task<AdminServiceResult<TransactionStateDto>> CompleteAsync(string id);
    Task<AdminServiceResult<TransactionStateDto>> FailAsync(string id, string? reason);
    Task<AdminServiceResult<TransactionStateDto>> RejectAsync(string id);
    Task<AdminServiceResult<TransactionStateDto>> CancelAsync(string id);
    Task<AdminServiceResult<TransactionStateDto>> TimeoutAsync(string id);
}

public sealed class AdminTransactionService : IAdminTransactionService
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<AdminTransactionService> _logger;
    private readonly IHubContext<AdminHub> _hub;
    private readonly WorkspaceContext _workspace;
    private readonly IBalanceService _balanceService;
    private readonly IAddressGenerator _addressGenerator;

    public AdminTransactionService(
        FireblocksDbContext context,
        ILogger<AdminTransactionService> logger,
        IHubContext<AdminHub> hub,
        WorkspaceContext workspace,
        IBalanceService balanceService,
        IAddressGenerator addressGenerator)
    {
        _context = context;
        _logger = logger;
        _hub = hub;
        _workspace = workspace;
        _balanceService = balanceService;
        _addressGenerator = addressGenerator;
    }

    public async Task<AdminServiceResult<List<AdminTransactionDto>>> GetTransactionsAsync()
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return WorkspaceRequired<List<AdminTransactionDto>>();
        }

        var transactions = await _context.Transactions
            .Where(t => t.WorkspaceId == _workspace.WorkspaceId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var vaultNameLookup = await BuildVaultNameLookupAsync(transactions);
        var dtos = transactions.Select(t => MapToDto(t, vaultNameLookup)).ToList();
        return Success(dtos);
    }

    public async Task<AdminServiceResult<AdminTransactionsPageDto>> GetTransactionsPagedAsync(
        int pageIndex,
        int pageSize,
        string? assetId,
        string? transactionId,
        string? hash)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return WorkspaceRequired<AdminTransactionsPageDto>();
        }

        var safePageIndex = Math.Max(0, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.Transactions
            .Where(t => t.WorkspaceId == _workspace.WorkspaceId);

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

        return Success(page);
    }

    public async Task<AdminServiceResult<AdminTransactionDto>> GetTransactionAsync(string id)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return WorkspaceRequired<AdminTransactionDto>();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.WorkspaceId == _workspace.WorkspaceId);

        if (transaction == null)
        {
            return NotFound<AdminTransactionDto>($"Transaction {id} not found", "TRANSACTION_NOT_FOUND");
        }

        var vaultNameLookup = await BuildVaultNameLookupAsync(transaction);
        return Success(MapToDto(transaction, vaultNameLookup));
    }

    public async Task<AdminServiceResult<AdminTransactionDto>> CreateTransactionAsync(CreateAdminTransactionRequestDto request)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return WorkspaceRequired<AdminTransactionDto>();
        }

        var asset = await _context.Assets.FindAsync(request.AssetId);
        if (asset == null)
        {
            return Failure<AdminTransactionDto>($"Asset {request.AssetId} not found", "ASSET_NOT_FOUND");
        }

        if (!decimal.TryParse(request.Amount, out var amount) || amount <= 0)
        {
            return Failure<AdminTransactionDto>("Invalid amount", "INVALID_AMOUNT");
        }

        var sourceType = NormalizeEndpointType(request.SourceType);
        var destinationType = NormalizeEndpointType(request.DestinationType);

        var sourceInternal = sourceType == "INTERNAL";
        var destinationInternal = destinationType == "INTERNAL";

        if (!sourceInternal && !destinationInternal)
        {
            return Failure<AdminTransactionDto>(
                "At least one side of the transaction must be INTERNAL",
                "INVALID_TRANSACTION_SCOPE");
        }

        var sourceVaultId = request.SourceVaultAccountId;
        var destinationVaultId = request.DestinationVaultAccountId;

        if (sourceInternal)
        {
            if (string.IsNullOrWhiteSpace(sourceVaultId))
            {
                return Failure<AdminTransactionDto>(
                    "Source vault account is required for INTERNAL source",
                    "SOURCE_VAULT_REQUIRED");
            }

            var sourceVault = await _context.VaultAccounts.FindAsync(sourceVaultId);
            if (sourceVault == null || sourceVault.WorkspaceId != _workspace.WorkspaceId)
            {
                return Failure<AdminTransactionDto>($"Vault account {sourceVaultId} not found", "VAULT_NOT_FOUND");
            }
        }

        if (destinationInternal)
        {
            if (string.IsNullOrWhiteSpace(destinationVaultId))
            {
                return Failure<AdminTransactionDto>(
                    "Destination vault account is required for INTERNAL destination",
                    "DESTINATION_VAULT_REQUIRED");
            }

            var destinationVault = await _context.VaultAccounts.FindAsync(destinationVaultId);
            if (destinationVault == null || destinationVault.WorkspaceId != _workspace.WorkspaceId)
            {
                return Failure<AdminTransactionDto>($"Vault account {destinationVaultId} not found", "VAULT_NOT_FOUND");
            }
        }

        var transactionVaultId = sourceInternal ? sourceVaultId : destinationVaultId;
        if (string.IsNullOrWhiteSpace(transactionVaultId))
        {
            return Failure<AdminTransactionDto>("Transaction requires an internal vault account", "VAULT_REQUIRED");
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
            WorkspaceId = _workspace.WorkspaceId,
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
            Hash = Guid.NewGuid().ToString(),
        };

        var derivedType = sourceInternal && !destinationInternal
            ? "OUTGOING"
            : !sourceInternal && destinationInternal
                ? "INCOMING"
                : "TRANSFER";

        if (derivedType == "INCOMING")
        {
            transaction.State = ResolveInitialState(request.InitialState, TransactionState.COMPLETED);
            if (transaction.State == TransactionState.COMPLETED)
            {
                transaction.Confirmations = 6;
                await _balanceService.CreditIncomingAsync(transaction);
            }

            _logger.LogInformation("Created INCOMING transaction {TxId} for {Amount} {AssetId}",
                transaction.Id, amount, request.AssetId);
        }
        else
        {
            var reserveResult = await _balanceService.ReserveFundsAsync(transaction);
            if (!reserveResult.Success)
            {
                return Failure<AdminTransactionDto>(reserveResult.ErrorMessage!, reserveResult.ErrorCode!);
            }

            transaction.State = ResolveInitialState(request.InitialState, TransactionState.SUBMITTED);

            _logger.LogInformation("Created OUTGOING transaction {TxId} in state {State}",
                transaction.Id, transaction.State);
        }

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        var vaultNameLookup = await BuildVaultNameLookupAsync(transaction);
        var dto = MapToDto(transaction, vaultNameLookup);
        await _hub.Clients.Group(_workspace.WorkspaceId).SendAsync("transactionUpserted", dto);
        await _hub.Clients.Group(_workspace.WorkspaceId).SendAsync("transactionsUpdated");

        return Success(dto);
    }

    public Task<AdminServiceResult<TransactionStateDto>> ApproveAsync(string id)
    {
        return TransitionTransactionAsync(id, TransactionState.PENDING_AUTHORIZATION);
    }

    public Task<AdminServiceResult<TransactionStateDto>> SignAsync(string id)
    {
        return TransitionTransactionAsync(id, TransactionState.QUEUED);
    }

    public async Task<AdminServiceResult<TransactionStateDto>> BroadcastAsync(string id)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return WorkspaceRequired<TransactionStateDto>();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.WorkspaceId == _workspace.WorkspaceId);
        if (transaction == null)
        {
            return NotFound<TransactionStateDto>($"Transaction {id} not found", "TRANSACTION_NOT_FOUND");
        }

        if (string.IsNullOrEmpty(transaction.Hash))
        {
            transaction.Hash = $"0x{Guid.NewGuid():N}";
        }

        return await TransitionTransactionAsync(transaction, TransactionState.BROADCASTING);
    }

    public async Task<AdminServiceResult<TransactionStateDto>> ConfirmAsync(string id)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return WorkspaceRequired<TransactionStateDto>();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.WorkspaceId == _workspace.WorkspaceId);
        if (transaction == null)
        {
            return NotFound<TransactionStateDto>($"Transaction {id} not found", "TRANSACTION_NOT_FOUND");
        }

        transaction.Confirmations++;
        return await TransitionTransactionAsync(transaction, TransactionState.CONFIRMING);
    }

    public async Task<AdminServiceResult<TransactionStateDto>> CompleteAsync(string id)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return WorkspaceRequired<TransactionStateDto>();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.WorkspaceId == _workspace.WorkspaceId);
        if (transaction == null)
        {
            return NotFound<TransactionStateDto>($"Transaction {id} not found", "TRANSACTION_NOT_FOUND");
        }

        if (transaction.Confirmations == 0)
        {
            transaction.Confirmations = 6;
        }

        await _balanceService.CompleteTransactionAsync(transaction);
        return await TransitionTransactionAsync(transaction, TransactionState.COMPLETED);
    }

    public async Task<AdminServiceResult<TransactionStateDto>> FailAsync(string id, string? reason)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return WorkspaceRequired<TransactionStateDto>();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.WorkspaceId == _workspace.WorkspaceId);
        if (transaction == null)
        {
            return NotFound<TransactionStateDto>($"Transaction {id} not found", "TRANSACTION_NOT_FOUND");
        }

        if (transaction.State.IsTerminal())
        {
            return Failure<TransactionStateDto>(
                $"Cannot fail transaction in terminal state {transaction.State}",
                "INVALID_STATE_TRANSITION");
        }

        await _balanceService.RollbackTransactionAsync(transaction);

        transaction.State = TransactionState.FAILED;
        transaction.FailureReason = reason ?? "NETWORK_ERROR";
        transaction.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        var vaultNameLookup = await BuildVaultNameLookupAsync(transaction);
        await _hub.Clients.Group(_workspace.WorkspaceId!).SendAsync("transactionUpserted", MapToDto(transaction, vaultNameLookup));
        await _hub.Clients.Group(_workspace.WorkspaceId!).SendAsync("transactionsUpdated");

        _logger.LogInformation("Failed transaction {TxId} with reason {Reason}",
            id, transaction.FailureReason);

        var result = new TransactionStateDto
        {
            Id = transaction.Id,
            State = transaction.State.ToString(),
        };

        return Success(result);
    }

    public Task<AdminServiceResult<TransactionStateDto>> RejectAsync(string id)
    {
        return TransitionTransactionAsync(id, TransactionState.REJECTED);
    }

    public Task<AdminServiceResult<TransactionStateDto>> CancelAsync(string id)
    {
        return TransitionTransactionAsync(id, TransactionState.CANCELLED);
    }

    public Task<AdminServiceResult<TransactionStateDto>> TimeoutAsync(string id)
    {
        return TransitionTransactionAsync(id, TransactionState.TIMEOUT);
    }

    private async Task<AdminServiceResult<TransactionStateDto>> TransitionTransactionAsync(string id, TransactionState newState)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return WorkspaceRequired<TransactionStateDto>();
        }

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.WorkspaceId == _workspace.WorkspaceId);
        if (transaction == null)
        {
            return NotFound<TransactionStateDto>($"Transaction {id} not found", "TRANSACTION_NOT_FOUND");
        }

        return await TransitionTransactionAsync(transaction, newState);
    }

    private async Task<AdminServiceResult<TransactionStateDto>> TransitionTransactionAsync(Transaction transaction, TransactionState newState)
    {
        if (transaction.State == newState)
        {
            _logger.LogInformation("Transaction {TxId} already in state {State}",
                transaction.Id, newState);

            var existingResult = new TransactionStateDto
            {
                Id = transaction.Id,
                State = transaction.State.ToString(),
            };

            return Success(existingResult);
        }

        if (!transaction.State.CanTransitionTo(newState))
        {
            return Failure<TransactionStateDto>(
                $"Invalid transition from {transaction.State} to {newState}",
                "INVALID_STATE_TRANSITION");
        }

        if (newState == TransactionState.REJECTED ||
            newState == TransactionState.CANCELLED ||
            newState == TransactionState.TIMEOUT)
        {
            await _balanceService.RollbackTransactionAsync(transaction);
        }

        transaction.TransitionTo(newState);
        await _context.SaveChangesAsync();
        var vaultNameLookup = await BuildVaultNameLookupAsync(transaction);
        await _hub.Clients.Group(_workspace.WorkspaceId!).SendAsync("transactionUpserted", MapToDto(transaction, vaultNameLookup));
        await _hub.Clients.Group(_workspace.WorkspaceId!).SendAsync("transactionsUpdated");

        _logger.LogInformation("Transitioned transaction {TxId} from {OldState} to {NewState}",
            transaction.Id, transaction.State, newState);

        var result = new TransactionStateDto
        {
            Id = transaction.Id,
            State = transaction.State.ToString(),
        };

        return Success(result);
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
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId && w.VaultAccount.WorkspaceId == _workspace.WorkspaceId);

        if (wallet == null)
        {
            var vault = await _context.VaultAccounts
                .FirstOrDefaultAsync(v => v.Id == vaultAccountId && v.WorkspaceId == _workspace.WorkspaceId);
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

    private static AdminServiceResult<T> Success<T>(T data)
    {
        return new AdminServiceResult<T>(AdminResponse<T>.Success(data), StatusCodes.Status200OK);
    }

    private static AdminServiceResult<T> Failure<T>(string message, string code)
    {
        return new AdminServiceResult<T>(AdminResponse<T>.Failure(message, code), StatusCodes.Status400BadRequest);
    }

    private static AdminServiceResult<T> NotFound<T>(string message, string code)
    {
        return new AdminServiceResult<T>(AdminResponse<T>.Failure(message, code), StatusCodes.Status404NotFound);
    }

    private static AdminServiceResult<T> WorkspaceRequired<T>()
    {
        return new AdminServiceResult<T>(AdminResponse<T>.Failure("Workspace is required", "WORKSPACE_REQUIRED"), StatusCodes.Status400BadRequest);
    }
}
