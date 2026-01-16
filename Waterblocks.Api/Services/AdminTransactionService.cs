using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Dtos.Admin;
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

public sealed class AdminTransactionService : AdminServiceBase, IAdminTransactionService
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<AdminTransactionService> _logger;
    private readonly IBalanceService _balanceService;
    private readonly ITransactionViewService _transactionView;
    private readonly IAddressGenerator _addressGenerator;
    private readonly ITransactionIdResolver _transactionIdResolver;
    private readonly IAdminTransactionMapper _transactionMapper;
    private readonly IAdminTransactionNotifier _transactionNotifier;
    private readonly IAdminTransactionTransitioner _transactionTransitioner;

    public AdminTransactionService(
        FireblocksDbContext context,
        ILogger<AdminTransactionService> logger,
        WorkspaceContext workspace,
        IBalanceService balanceService,
        IAddressGenerator addressGenerator,
        ITransactionViewService transactionView,
        ITransactionIdResolver transactionIdResolver,
        IAdminTransactionMapper transactionMapper,
        IAdminTransactionNotifier transactionNotifier,
        IAdminTransactionTransitioner transactionTransitioner)
        : base(workspace)
    {
        _context = context;
        _logger = logger;
        _balanceService = balanceService;
        _addressGenerator = addressGenerator;
        _transactionView = transactionView;
        _transactionIdResolver = transactionIdResolver;
        _transactionMapper = transactionMapper;
        _transactionNotifier = transactionNotifier;
        _transactionTransitioner = transactionTransitioner;
    }

    public async Task<AdminServiceResult<List<AdminTransactionDto>>> GetTransactionsAsync()
    {
        if (!TryGetWorkspaceId<List<AdminTransactionDto>>(out var workspaceId, out var failure))
        {
            return failure;
        }

        // Get all addresses belonging to vaults in the current workspace
        var workspaceAddresses = await _transactionView.GetWorkspaceAddressesAsync(workspaceId);

        // Find transactions where source OR destination address belongs to this workspace
        var transactions = await _transactionView.ApplyWorkspaceAddressFilter(_context.Transactions, workspaceAddresses)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var dtos = await _transactionMapper.MapAsync(transactions, workspaceId);
        return Success(dtos);
    }

    public async Task<AdminServiceResult<AdminTransactionsPageDto>> GetTransactionsPagedAsync(
        int pageIndex,
        int pageSize,
        string? assetId,
        string? transactionId,
        string? hash)
    {
        if (!TryGetWorkspaceId<AdminTransactionsPageDto>(out var workspaceId, out var failure))
        {
            return failure;
        }

        var safePageIndex = Math.Max(0, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 200);

        // Get all addresses belonging to vaults in the current workspace
        var workspaceAddresses = await _transactionView.GetWorkspaceAddressesAsync(workspaceId);

        // Find transactions where source OR destination address belongs to this workspace
        var query = _transactionView.ApplyWorkspaceAddressFilter(_context.Transactions, workspaceAddresses);

        var normalizedAsset = assetId?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedAsset))
        {
            var assetLower = normalizedAsset.ToLowerInvariant();
            query = query.Where(t => t.AssetId.ToLower().Contains(assetLower));
        }

        var normalizedId = transactionId?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedId))
        {
            if (!_transactionIdResolver.TryUnwrap(normalizedId, out var rawId))
            {
                query = query.Where(_ => false);
            }
            else
            {
                var idLower = rawId.ToLowerInvariant();
                query = query.Where(t => t.Id.ToLower().Contains(idLower));
            }
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

        var dtos = await _transactionMapper.MapAsync(transactions, workspaceId);

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
        if (!TryGetWorkspaceId<AdminTransactionDto>(out var workspaceId, out var failure))
        {
            return failure;
        }

        var transaction = await _transactionIdResolver.FindWorkspaceTransactionAsync(id);
        if (transaction == null)
        {
            return NotFound<AdminTransactionDto>($"Transaction {id} not found", "TRANSACTION_NOT_FOUND");
        }

        return Success(await _transactionMapper.MapAsync(transaction, workspaceId));
    }

    public async Task<AdminServiceResult<AdminTransactionDto>> CreateTransactionAsync(CreateAdminTransactionRequestDto request)
    {
        if (!TryGetWorkspaceId<AdminTransactionDto>(out var workspaceId, out var failure))
        {
            return failure;
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

        var addressLookup = await _transactionView.BuildAddressOwnershipLookupAsync(
            request.AssetId,
            new[] { sourceAddress, destinationAddress },
            workspaceId);

        var sourceOwnership = _transactionView.ResolveOwnership(addressLookup, request.AssetId, sourceAddress);
        var destinationOwnership = _transactionView.ResolveOwnership(addressLookup, request.AssetId, destinationAddress);

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
            WorkspaceId = workspaceId,
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
        var dto = await _transactionNotifier.NotifyUpsertAsync(transaction, workspaceId);
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
        if (!TryGetWorkspaceId<TransactionStateDto>(out var workspaceId, out var failure))
        {
            return failure;
        }

        var transaction = await _transactionIdResolver.FindWorkspaceTransactionAsync(id);
        if (transaction == null)
        {
            return NotFound<TransactionStateDto>($"Transaction {id} not found", "TRANSACTION_NOT_FOUND");
        }

        if (string.IsNullOrEmpty(transaction.Hash))
        {
            transaction.Hash = $"0x{Guid.NewGuid():N}";
        }

        return await TransitionTransactionAsync(transaction, TransactionState.BROADCASTING, workspaceId);
    }

    public async Task<AdminServiceResult<TransactionStateDto>> ConfirmAsync(string id)
    {
        if (!TryGetWorkspaceId<TransactionStateDto>(out var workspaceId, out var failure))
        {
            return failure;
        }

        var transaction = await _transactionIdResolver.FindWorkspaceTransactionAsync(id);
        if (transaction == null)
        {
            return NotFound<TransactionStateDto>($"Transaction {id} not found", "TRANSACTION_NOT_FOUND");
        }

        transaction.Confirmations++;
        return await TransitionTransactionAsync(transaction, TransactionState.CONFIRMING, workspaceId);
    }

    public async Task<AdminServiceResult<TransactionStateDto>> CompleteAsync(string id)
    {
        if (!TryGetWorkspaceId<TransactionStateDto>(out var workspaceId, out var failure))
        {
            return failure;
        }

        var transaction = await _transactionIdResolver.FindWorkspaceTransactionAsync(id);
        if (transaction == null)
        {
            return NotFound<TransactionStateDto>($"Transaction {id} not found", "TRANSACTION_NOT_FOUND");
        }

        if (transaction.Confirmations == 0)
        {
            transaction.Confirmations = 6;
        }

        await _balanceService.CompleteTransactionAsync(transaction);
        return await TransitionTransactionAsync(transaction, TransactionState.COMPLETED, workspaceId);
    }

    public async Task<AdminServiceResult<TransactionStateDto>> FailAsync(string id, string? reason)
    {
        if (!TryGetWorkspaceId<TransactionStateDto>(out var workspaceId, out var failure))
        {
            return failure;
        }

        var transaction = await _transactionIdResolver.FindWorkspaceTransactionAsync(id);
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
        await _transactionNotifier.NotifyUpsertAsync(transaction, workspaceId);

        _logger.LogInformation("Failed transaction {TxId} with reason {Reason}",
            id, transaction.FailureReason);

        var result = new TransactionStateDto
        {
            Id = TransactionCompositeId.Build(workspaceId, transaction.Id),
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
        if (!TryGetWorkspaceId<TransactionStateDto>(out var workspaceId, out var failure))
        {
            return failure;
        }

        var transaction = await _transactionIdResolver.FindWorkspaceTransactionAsync(id);
        if (transaction == null)
        {
            return NotFound<TransactionStateDto>($"Transaction {id} not found", "TRANSACTION_NOT_FOUND");
        }

        return await TransitionTransactionAsync(transaction, newState, workspaceId);
    }

    private async Task<AdminServiceResult<TransactionStateDto>> TransitionTransactionAsync(
        Transaction transaction,
        TransactionState newState,
        string workspaceId)
    {
        var outcome = await _transactionTransitioner.TransitionAsync(transaction, newState, workspaceId);
        if (!outcome.Success)
        {
            return Failure<TransactionStateDto>(outcome.ErrorMessage!, outcome.ErrorCode!);
        }

        return Success(outcome.Result!);
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

}



