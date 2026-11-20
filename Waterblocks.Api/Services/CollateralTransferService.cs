using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Dtos.Fireblocks;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Models;
using Waterblocks.Api.Utils;

namespace Waterblocks.Api.Services;

public sealed record CollateralTransferCommand(
    string? ExchangeAccountId,
    string? VaultAccountId,
    string? AssetId,
    string? Amount,
    bool TreatAsGrossAmount,
    string? Note,
    string? ExternalTxId,
    string? CustomerRefId,
    string? InitiatedBy);

/// <summary>
/// Creates Add collateral and Remove collateral transactions for both API surfaces.
/// </summary>
public interface ICollateralTransferService
{
    Task<Transaction> AddAsync(CollateralTransferCommand command);
    Task<Transaction> RemoveAsync(CollateralTransferCommand command);
}

public sealed class CollateralTransferService : ICollateralTransferService
{
    private const string RejectedByExchange = "3RD_PARTY_REJECTED";
    private const int MaxNoteLength = 1000;
    private const int MaxReferenceLength = 100;
    private const int MaxInitiatedByLength = 500;

    private readonly WorkspaceContext _workspace;
    private readonly ICollateralTransactionRunner _runner;
    private readonly IExchangeCatalog _catalog;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly IdempotencyContext _idempotency;
    private readonly ILogger<CollateralTransferService> _logger;

    public CollateralTransferService(
        WorkspaceContext workspace,
        ICollateralTransactionRunner runner,
        IExchangeCatalog catalog,
        IRealtimeNotifier realtimeNotifier,
        IdempotencyContext idempotency,
        ILogger<CollateralTransferService> logger)
    {
        _workspace = workspace;
        _runner = runner;
        _catalog = catalog;
        _realtimeNotifier = realtimeNotifier;
        _idempotency = idempotency;
        _logger = logger;
    }

    public Task<Transaction> AddAsync(CollateralTransferCommand command)
    {
        return CreateAsync(command, CollateralDirection.ADD);
    }

    public Task<Transaction> RemoveAsync(CollateralTransferCommand command)
    {
        return CreateAsync(command, CollateralDirection.REMOVE);
    }

    private async Task<Transaction> CreateAsync(CollateralTransferCommand command, CollateralDirection direction)
    {
        var workspaceId = _workspace.WorkspaceId;
        if (string.IsNullOrEmpty(workspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        ValidateCommand(command);

        var transactionId = Guid.NewGuid().ToString();
        var transaction = await _runner.RunAsync(
            unitOfWork => BuildTransactionAsync(unitOfWork, command, direction, workspaceId, transactionId));
        _idempotency.MarkReplyStored();

        await _realtimeNotifier.NotifyTransactionCreatedAsync(workspaceId);
        await _realtimeNotifier.NotifyPendingTransactionsUpdatedAsync();

        _logger.LogInformation(
            "Created collateral transfer {TransactionId} ({Direction}) for {Amount} {AssetId} in state {State}",
            transaction.Id, direction, transaction.Amount, transaction.AssetId, transaction.State);

        return transaction;
    }

    private async Task<Transaction> BuildTransactionAsync(
        CollateralUnitOfWork unitOfWork,
        CollateralTransferCommand command,
        CollateralDirection direction,
        string workspaceId,
        string transactionId)
    {
        // A retry after a lost commit must not create a second transaction.
        var committed = await unitOfWork.Database.Transactions.FindAsync(transactionId);
        if (committed != null)
        {
            return committed;
        }

        var account = await RequireExchangeAccountAsync(unitOfWork, command.ExchangeAccountId!, workspaceId);
        var vault = await RequireVaultAsync(unitOfWork, command.VaultAccountId!, workspaceId);
        await unitOfWork.LockVaultWalletsAsync(vault.Id);
        var asset = await unitOfWork.Transactions.RequireAssetAsync(command.AssetId!);
        await RequireSupportedAssetAsync(account, asset, direction);

        var requestedAmount = unitOfWork.Transactions.ParseRequestedAmount(command.Amount);
        DecimalColumnGuard.EnsureNumeric36Scale18(requestedAmount, "Amount");
        await unitOfWork.Transactions.EnsureExternalTxIdIsFreeAsync(command.ExternalTxId);
        var amounts = unitOfWork.Transactions.CalculateAmounts(asset, requestedAmount, command.TreatAsGrossAmount);
        var collateralRow = await unitOfWork.Ledger.EnsureAssetRowAsync(account.Id, asset.AssetId);

        string sourceAddress;
        string destinationAddress;
        string? destinationTag = null;
        if (direction == CollateralDirection.ADD)
        {
            DecimalColumnGuard.EnsureNumeric36Scale18(
                collateralRow.Custody + amounts.TransferAmount,
                ExchangeCollateralLedger.CollateralTotalName(asset.AssetId));
            sourceAddress = await unitOfWork.Transactions.RequireSourceAddressAsync(vault.Id, asset, workspaceId);
            destinationAddress = collateralRow.CollateralAddress;
        }
        else
        {
            var wallet = await unitOfWork.Transactions.GetOrCreateDestinationWalletAsync(vault.Id, asset, workspaceId);
            var vaultAddress = FirstListedAddress(wallet);
            sourceAddress = collateralRow.CollateralAddress;
            destinationAddress = vaultAddress.AddressValue;
            destinationTag = asset.BlockchainType == BlockchainType.MemoBased ? vaultAddress.Tag : null;
        }

        var transaction = unitOfWork.Transactions.Create(new TransactionDraft
        {
            Id = transactionId,
            VaultAccountId = vault.Id,
            WorkspaceId = workspaceId,
            Asset = asset,
            SourceAddress = sourceAddress,
            DestinationAddress = destinationAddress,
            DestinationTag = destinationTag,
            Amounts = amounts,
            TreatAsGrossAmount = command.TreatAsGrossAmount,
            Note = command.Note,
            ExternalTxId = command.ExternalTxId,
            CustomerRefId = command.CustomerRefId,
            InitiatedBy = command.InitiatedBy ?? _workspace.ApiKey,
        });
        transaction.ExchangeAccountId = account.Id;
        transaction.ExchangeAccountName = account.Name;
        transaction.ExchangeType = account.ExchangeType;
        transaction.CollateralDirection = direction;

        var reservation = await unitOfWork.Balance.ReserveFundsAsync(transaction);
        if (!reservation.Success)
        {
            if (direction == CollateralDirection.ADD)
            {
                throw ExchangeOperationException.BadRequest(reservation.ErrorCode!, reservation.ErrorMessage!);
            }

            transaction.State = TransactionState.CANCELLED;
            transaction.SubStatus = RejectedByExchange;
            transaction.FailureReason = reservation.ErrorMessage;
        }

        unitOfWork.Database.Transactions.Add(transaction);
        _idempotency.StageReply(
            unitOfWork.Database,
            StatusCodes.Status200OK,
            new CreateTransactionResponseDto
            {
                Id = TransactionCompositeId.Build(workspaceId, transaction.Id),
                Status = transaction.State.ToString(),
            });

        return transaction;
    }

    /// <summary>
    /// Returns the first address of the wallet in the order of the Fireblocks address list: the BIP44 index first, with
    /// addresses that have no index last, then the id.
    /// </summary>
    private static Address FirstListedAddress(Wallet wallet)
    {
        return wallet.Addresses
            .OrderBy(address => address.Bip44AddressIndex ?? int.MaxValue)
            .ThenBy(address => address.Id)
            .First();
    }

    private static void ValidateCommand(CollateralTransferCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ExchangeAccountId))
        {
            throw new ArgumentException("Exchange account id is required");
        }

        if (string.IsNullOrWhiteSpace(command.VaultAccountId))
        {
            throw new ArgumentException("Vault account id is required");
        }

        if (string.IsNullOrWhiteSpace(command.AssetId))
        {
            throw new ArgumentException("AssetId is required");
        }

        RequireMaxLength(command.Note, MaxNoteLength, "Note");
        RequireMaxLength(command.ExternalTxId, MaxReferenceLength, "externalTxId");
        RequireMaxLength(command.CustomerRefId, MaxReferenceLength, "customerRefId");
        RequireMaxLength(command.InitiatedBy, MaxInitiatedByLength, "initiatedBy");
    }

    private static void RequireMaxLength(string? value, int maxLength, string fieldName)
    {
        if (value?.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} must have at most {maxLength} characters");
        }
    }

    private async Task RequireSupportedAssetAsync(ExchangeAccount account, Asset asset, CollateralDirection direction)
    {
        if (account.IsConnected)
        {
            var exchange = await _catalog.RequireAsync(account.ExchangeType);
            if (exchange.Currencies.All(currency => currency.AssetId != asset.AssetId))
            {
                throw ExchangeOperationException.BadRequest(
                    "ASSET_NOT_SUPPORTED",
                    $"A connected {exchange.Name} account does not accept {asset.AssetId}");
            }
        }

        if (direction == CollateralDirection.REMOVE && asset.HasSeparateFeeAsset())
        {
            throw ExchangeOperationException.BadRequest(
                "ASSET_NOT_SUPPORTED",
                $"Remove collateral needs an asset that pays its fee in the same asset. {asset.AssetId} pays in {asset.GetFeeAssetId()}");
        }
    }

    private static async Task<ExchangeAccount> RequireExchangeAccountAsync(
        CollateralUnitOfWork unitOfWork,
        string exchangeAccountId,
        string workspaceId)
    {
        return await unitOfWork.Database.ExchangeAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(account => account.Id == exchangeAccountId && account.WorkspaceId == workspaceId)
            ?? throw ExchangeOperationException.NotFound(
                "EXCHANGE_ACCOUNT_NOT_FOUND",
                $"Exchange account {exchangeAccountId} not found");
    }

    private static async Task<VaultAccount> RequireVaultAsync(
        CollateralUnitOfWork unitOfWork,
        string vaultAccountId,
        string workspaceId)
    {
        return await unitOfWork.Database.VaultAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(vault => vault.Id == vaultAccountId && vault.WorkspaceId == workspaceId)
            ?? throw ExchangeOperationException.NotFound(
                "VAULT_NOT_FOUND",
                $"Vault account {vaultAccountId} not found");
    }
}
