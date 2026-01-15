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

        var addressLookup = await BuildAddressOwnershipLookupAsync(transactions);
        var dtos = transactions.Select(t => MapToDto(t, addressLookup)).ToList();
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

        var addressLookup = await BuildAddressOwnershipLookupAsync(transactions);
        var dtos = transactions.Select(t => MapToDto(t, addressLookup)).ToList();

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

        var addressLookup = await BuildAddressOwnershipLookupAsync(new[] { transaction });
        return Success(MapToDto(transaction, addressLookup));
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

        var sourceAddress = request.SourceAddress?.Trim();
        var destinationAddress = request.DestinationAddress?.Trim();

        if (string.IsNullOrWhiteSpace(sourceAddress))
        {
            sourceAddress = _addressGenerator.GenerateExternalAddress(request.AssetId);
        }

        if (string.IsNullOrWhiteSpace(destinationAddress))
        {
            destinationAddress = _addressGenerator.GenerateExternalAddress(request.AssetId);
        }

        if (string.IsNullOrWhiteSpace(sourceAddress))
        {
            return Failure<AdminTransactionDto>("Source address is required", "SOURCE_ADDRESS_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(destinationAddress))
        {
            return Failure<AdminTransactionDto>("Destination address is required", "DESTINATION_ADDRESS_REQUIRED");
        }

        var addressLookup = await BuildAddressOwnershipLookupAsync(
            request.AssetId,
            new[] { sourceAddress, destinationAddress });

        var sourceOwnership = ResolveAddressOwnership(addressLookup, request.AssetId, sourceAddress);
        var destinationOwnership = ResolveAddressOwnership(addressLookup, request.AssetId, destinationAddress);

        var sourceInternal = sourceOwnership != null;
        var destinationInternal = destinationOwnership != null;

        if (!sourceInternal && !destinationInternal)
        {
            return Failure<AdminTransactionDto>(
                "At least one side of the transaction must be INTERNAL",
                "INVALID_TRANSACTION_SCOPE");
        }

        var transactionVaultId = sourceInternal
            ? sourceOwnership!.VaultAccountId
            : destinationOwnership!.VaultAccountId;

        var transaction = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            VaultAccountId = transactionVaultId!,
            WorkspaceId = _workspace.WorkspaceId,
            AssetId = request.AssetId,
            SourceAddress = sourceAddress,
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
        var responseAddressLookup = await BuildAddressOwnershipLookupAsync(new[] { transaction });
        var dto = MapToDto(transaction, responseAddressLookup);
        await _hub.Clients.Group(_workspace.WorkspaceId).SendAsync("transactionUpserted", dto);
        await _hub.Clients.Group(_workspace.WorkspaceId).SendAsync("transactionsUpdated");
        await _hub.Clients.Group(_workspace.WorkspaceId).SendAsync("vaultsUpdated");

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
        var addressLookup = await BuildAddressOwnershipLookupAsync(new[] { transaction });
        await _hub.Clients.Group(_workspace.WorkspaceId!).SendAsync("transactionUpserted", MapToDto(transaction, addressLookup));
        await _hub.Clients.Group(_workspace.WorkspaceId!).SendAsync("transactionsUpdated");
        await _hub.Clients.Group(_workspace.WorkspaceId).SendAsync("vaultsUpdated");

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
        var addressLookup = await BuildAddressOwnershipLookupAsync(new[] { transaction });
        await _hub.Clients.Group(_workspace.WorkspaceId!).SendAsync("transactionUpserted", MapToDto(transaction, addressLookup));
        await _hub.Clients.Group(_workspace.WorkspaceId!).SendAsync("transactionsUpdated");
        await _hub.Clients.Group(_workspace.WorkspaceId).SendAsync("vaultsUpdated");

        _logger.LogInformation("Transitioned transaction {TxId} from {OldState} to {NewState}",
            transaction.Id, transaction.State, newState);

        var result = new TransactionStateDto
        {
            Id = transaction.Id,
            State = transaction.State.ToString(),
        };

        return Success(result);
    }

    private AdminTransactionDto MapToDto(Transaction transaction, IReadOnlyDictionary<string, AddressOwnership> addressLookup)
    {
        var sourceOwnership = ResolveAddressOwnership(addressLookup, transaction.AssetId, transaction.SourceAddress);
        var destinationOwnership = ResolveAddressOwnership(addressLookup, transaction.AssetId, transaction.DestinationAddress);
        var sourceType = sourceOwnership != null ? "INTERNAL" : "EXTERNAL";
        var destinationType = destinationOwnership != null ? "INTERNAL" : "EXTERNAL";

        return new AdminTransactionDto
        {
            Id = transaction.Id,
            VaultAccountId = transaction.VaultAccountId,
            AssetId = transaction.AssetId,
            SourceType = sourceType,
            SourceAddress = transaction.SourceAddress,
            SourceVaultAccountName = sourceOwnership?.VaultAccountName,
            DestinationType = destinationType,
            DestinationVaultAccountName = destinationOwnership?.VaultAccountName,
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

    private Task<Dictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(Transaction transaction)
    {
        return BuildAddressOwnershipLookupAsync(new[] { transaction });
    }

    private async Task<Dictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(string assetId, IEnumerable<string> addresses)
    {
        var addressValues = addresses
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address.Trim())
            .Distinct()
            .ToList();

        if (addressValues.Count == 0)
        {
            return new Dictionary<string, AddressOwnership>();
        }

        var addressEntities = await _context.Addresses
            .Include(a => a.Wallet)
            .ThenInclude(w => w.VaultAccount)
            .Where(a => addressValues.Contains(a.AddressValue)
                        && a.Wallet.AssetId == assetId
                        && a.Wallet.VaultAccount.WorkspaceId == _workspace.WorkspaceId)
            .ToListAsync();

        var lookup = new Dictionary<string, AddressOwnership>();
        foreach (var address in addressEntities)
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

    private static TransactionState ResolveInitialState(string? initialState, TransactionState fallback)
    {
        if (!string.IsNullOrEmpty(initialState) &&
            Enum.TryParse<TransactionState>(initialState, out var parsed))
        {
            return parsed;
        }

        return fallback;
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


