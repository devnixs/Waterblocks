using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Dtos.Fireblocks;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public interface ITransactionService
{
    Task<CreateTransactionResponseDto> CreateTransactionAsync(CreateTransactionRequestDto request);
}

public sealed class TransactionService : ITransactionService
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<TransactionService> _logger;
    private readonly WorkspaceContext _workspace;
    private readonly IBalanceService _balanceService;
    private readonly IAddressGenerator _addressGenerator;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public TransactionService(
        FireblocksDbContext context,
        ILogger<TransactionService> logger,
        WorkspaceContext workspace,
        IBalanceService balanceService,
        IAddressGenerator addressGenerator,
        IRealtimeNotifier realtimeNotifier)
    {
        _context = context;
        _logger = logger;
        _workspace = workspace;
        _balanceService = balanceService;
        _addressGenerator = addressGenerator;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<CreateTransactionResponseDto> CreateTransactionAsync(CreateTransactionRequestDto request)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        if (request.Source == null)
        {
            throw new ArgumentException("Source is required");
        }

        if (string.IsNullOrEmpty(request.AssetId))
        {
            throw new ArgumentException("AssetId is required");
        }

        var vaultAccountId = request.Source.Id;
        var vaultAccount = await _context.VaultAccounts
            .FirstOrDefaultAsync(v => v.Id == vaultAccountId && v.WorkspaceId == _workspace.WorkspaceId);
        if (vaultAccount == null)
        {
            throw new KeyNotFoundException($"Vault account {vaultAccountId} not found");
        }

        var asset = await _context.Assets.FindAsync(request.AssetId);
        if (asset == null)
        {
            throw new KeyNotFoundException($"Asset {request.AssetId} not found");
        }

        if (!decimal.TryParse(request.Amount, out var requestedAmount) || requestedAmount <= 0)
        {
            throw new ArgumentException("Invalid amount");
        }

        if (!string.IsNullOrWhiteSpace(request.ExternalTxId))
        {
            var exists = await _context.Transactions
                .AnyAsync(t => t.ExternalTxId == request.ExternalTxId);
            if (exists)
            {
                throw new DuplicateExternalTxIdException(request.ExternalTxId);
            }
        }

        // Get source address from source vault
        var sourceWallet = await _context.Wallets
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == request.AssetId);

        if (sourceWallet == null)
        {
            throw new KeyNotFoundException($"Wallet for vault {vaultAccountId} and asset {request.AssetId} not found");
        }

        // Auto-generate address if wallet has no addresses
        if (!sourceWallet.Addresses.Any())
        {
            var newAddress = new Address
            {
                AddressValue = _addressGenerator.GenerateVaultWalletDepositAddress(request.AssetId, vaultAccountId),
                Type = "Permanent",
                WalletId = sourceWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _context.Addresses.Add(newAddress);
            await _context.SaveChangesAsync();
            sourceWallet.Addresses.Add(newAddress);

            _logger.LogInformation("Auto-generated address {Address} for vault {VaultId} asset {AssetId}",
                newAddress.AddressValue, vaultAccountId, request.AssetId);
        }

        var sourceAddress = sourceWallet.Addresses.First().AddressValue;

        // Get or validate destination address
        var destinationAddress = string.Empty;
        var destinationVaultId = string.Empty;
        var destinationWorkspaceId = string.Empty;

        if (request.Destination?.Type == TransferPeerType.VAULT_ACCOUNT && !string.IsNullOrEmpty(request.Destination.Id))
        {
            // Destination is a vault
            destinationVaultId = request.Destination.Id;
            var destinationWallet = await _context.Wallets
                .Include(w => w.Addresses)
                .Include(w => w.VaultAccount)
                .FirstOrDefaultAsync(w => w.VaultAccountId == destinationVaultId && w.AssetId == request.AssetId);

            if (destinationWallet == null)
            {
                throw new KeyNotFoundException($"Wallet for vault {destinationVaultId} and asset {request.AssetId} not found");
            }

            // Auto-generate address if wallet has no addresses
            if (!destinationWallet.Addresses.Any())
            {
                var newAddress = new Address
                {
                    AddressValue = _addressGenerator.GenerateVaultWalletDepositAddress(request.AssetId, destinationVaultId),
                    Type = "Permanent",
                    WalletId = destinationWallet.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                _context.Addresses.Add(newAddress);
                await _context.SaveChangesAsync();
                destinationWallet.Addresses.Add(newAddress);

                _logger.LogInformation("Auto-generated address {Address} for vault {VaultId} asset {AssetId}",
                    newAddress.AddressValue, destinationVaultId, request.AssetId);
            }

            destinationWorkspaceId = destinationWallet.VaultAccount?.WorkspaceId ?? string.Empty;

            // Check if specific address is provided via OneTimeAddress (override)
            var explicitAddress = request.Destination.OneTimeAddress?.Address;
            if (!string.IsNullOrWhiteSpace(explicitAddress))
            {
                // Validate it belongs to this vault
                var addressBelongsToVault = destinationWallet.Addresses
                    .Any(a => a.AddressValue == explicitAddress);

                if (!addressBelongsToVault)
                {
                    throw new ArgumentException(
                        $"Address {explicitAddress} does not belong to vault {destinationVaultId}");
                }

                destinationAddress = explicitAddress;
            }
            else
            {
                // Use first address if not specified (default behavior)
                destinationAddress = destinationWallet.Addresses.First().AddressValue;
            }
        }
        else
        {
            // Destination is external address
            destinationAddress = request.Destination?.OneTimeAddress?.Address ?? string.Empty;
            if (!string.IsNullOrEmpty(destinationAddress) && !ValidateAddressFormat(request.AssetId, destinationAddress))
            {
                throw new ArgumentException($"Invalid destination address format for asset {request.AssetId}");
            }
        }

        // Final validation: ensure destination address is not empty
        if (string.IsNullOrWhiteSpace(destinationAddress))
        {
            throw new ArgumentException("Destination address is required");
        }

        // Final validation: ensure destination vault ID is set if type is VAULT_ACCOUNT
        if (request.Destination?.Type == TransferPeerType.VAULT_ACCOUNT && string.IsNullOrWhiteSpace(destinationVaultId))
        {
            throw new ArgumentException("Destination vault ID is required when destination type is VAULT_ACCOUNT");
        }

        var networkFee = asset.BaseFee;
        var feeCurrency = asset.GetFeeAssetId();
        var treatAsGrossAmount = request.TreatAsGrossAmount ?? false;

        decimal transferAmount;
        if (treatAsGrossAmount)
        {
            if (feeCurrency == request.AssetId)
            {
                transferAmount = requestedAmount - networkFee;
                if (transferAmount <= 0)
                {
                    throw new ArgumentException($"Amount ({requestedAmount}) must be greater than fee ({networkFee})");
                }
            }
            else
            {
                transferAmount = requestedAmount;
            }
        }
        else
        {
            transferAmount = requestedAmount;
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            VaultAccountId = vaultAccountId,
            WorkspaceId = _workspace.WorkspaceId,
            AssetId = request.AssetId,
            SourceAddress = sourceAddress,
            Amount = transferAmount,
            RequestedAmount = requestedAmount,
            NetworkFee = networkFee,
            Fee = networkFee,
            DestinationAddress = destinationAddress,
            DestinationTag = request.Destination?.OneTimeAddress?.Tag,
            State = TransactionState.SUBMITTED,
            Note = request.Note,
            ExternalTxId = request.ExternalTxId,
            CustomerRefId = request.CustomerRefId,
            Operation = request.Operation ?? "TRANSFER",
            FeeCurrency = feeCurrency,
            TreatAsGrossAmount = treatAsGrossAmount,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Hash = Guid.NewGuid().ToString(),
        };

        var reserveResult = await _balanceService.ReserveFundsAsync(transaction);
        if (!reserveResult.Success)
        {
            throw new InvalidOperationException(reserveResult.ErrorMessage);
        }

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        var affectedWorkspaces = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(_workspace.WorkspaceId))
        {
            affectedWorkspaces.Add(_workspace.WorkspaceId);
        }

        if (!string.IsNullOrWhiteSpace(destinationWorkspaceId))
        {
            affectedWorkspaces.Add(destinationWorkspaceId);
        }

        foreach (var workspaceId in affectedWorkspaces)
        {
            await _realtimeNotifier.NotifyTransactionsUpdatedAsync(workspaceId);
            await _realtimeNotifier.NotifyVaultsUpdatedAsync(workspaceId);
        }

        _logger.LogInformation("Created transaction {TransactionId} for {Amount} {AssetId} (requested: {RequestedAmount}, fee: {Fee} {FeeCurrency}) from vault {VaultAccountId}",
            transaction.Id, transferAmount, request.AssetId, requestedAmount, networkFee, feeCurrency, vaultAccountId);

        return new CreateTransactionResponseDto
        {
            Id = TransactionCompositeId.Build(_workspace.WorkspaceId, transaction.Id),
            Status = transaction.State.ToString(),
        };
    }

    private static bool ValidateAddressFormat(string assetId, string address)
    {
        return assetId switch
        {
            "BTC" => address.StartsWith("bc1") || address.StartsWith("1") || address.StartsWith("3"),
            "ETH" or "USDT" or "USDC" => address.StartsWith("0x") && address.Length == 42,
            _ => !string.IsNullOrWhiteSpace(address),
        };
    }
}







