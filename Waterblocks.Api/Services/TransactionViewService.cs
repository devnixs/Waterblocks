using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Dtos.Fireblocks;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public interface ITransactionViewService
{
    Task<HashSet<string>> GetWorkspaceAddressesAsync(string workspaceId);
    IQueryable<Transaction> ApplyWorkspaceAddressFilter(IQueryable<Transaction> query, string workspaceId);
    Task<IReadOnlyDictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(IEnumerable<Transaction> transactions, string workspaceId);
    Task<IReadOnlyDictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(IEnumerable<Transaction> transactions, IEnumerable<string> workspaceIds);
    Task<IReadOnlyDictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(string assetId, IEnumerable<string> addresses);
    AddressOwnership? ResolveOwnership(IReadOnlyDictionary<string, AddressOwnership> lookup, string assetId, string? address);
    TransactionDto MapToFireblocksDto(Transaction transaction, IReadOnlyDictionary<string, AddressOwnership> addressLookup, string? workspaceId);
    AdminTransactionDto MapToAdminDto(Transaction transaction, IReadOnlyDictionary<string, AddressOwnership> addressLookup, string? workspaceId);
}

public sealed class TransactionViewService : ITransactionViewService
{
    private readonly FireblocksDbContext _context;

    public TransactionViewService(FireblocksDbContext context)
    {
        _context = context;
    }

    public async Task<HashSet<string>> GetWorkspaceAddressesAsync(string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return new HashSet<string>();
        }

        var addresses = await _context.Addresses
            .Include(a => a.Wallet)
            .ThenInclude(w => w.VaultAccount)
            .Where(a => a.Wallet.VaultAccount.WorkspaceId == workspaceId)
            .Select(a => a.AddressValue)
            .ToListAsync();

        return addresses.ToHashSet();
    }

    public IQueryable<Transaction> ApplyWorkspaceAddressFilter(IQueryable<Transaction> query, string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return query.Where(_ => false);
        }

        return query.Where(t =>
            _context.Addresses
                .Join(
                    _context.Assets,
                    address => address.Wallet.AssetId,
                    asset => asset.AssetId,
                    (address, asset) => new { Address = address, Asset = asset })
                .Any(entry =>
                    entry.Address.Wallet.VaultAccount.WorkspaceId == workspaceId &&
                    entry.Address.Wallet.AssetId == t.AssetId &&
                    (entry.Asset.IsCaseSensitive
                        ? (entry.Address.AddressValue == t.SourceAddress ||
                           entry.Address.AddressValue == t.DestinationAddress)
                        : (entry.Address.AddressValue.ToLower() == (t.SourceAddress ?? string.Empty).ToLower() ||
                           entry.Address.AddressValue.ToLower() == (t.DestinationAddress ?? string.Empty).ToLower()))));
    }

    public Task<IReadOnlyDictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(
        IEnumerable<Transaction> transactions,
        string workspaceId)
    {
        return BuildAddressOwnershipLookupAsync(transactions, new[] { workspaceId });
    }

    public async Task<IReadOnlyDictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(
        IEnumerable<Transaction> transactions,
        IEnumerable<string> workspaceIds)
    {
        var transactionList = transactions.ToList();
        if (transactionList.Count == 0)
        {
            return new Dictionary<string, AddressOwnership>();
        }

        var workspaceIdList = workspaceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        if (workspaceIdList.Count == 0)
        {
            return new Dictionary<string, AddressOwnership>();
        }

        var assetIds = transactionList
            .Select(t => t.AssetId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (assetIds.Count == 0)
        {
            return new Dictionary<string, AddressOwnership>();
        }

        var assetPolicies = await _context.Assets
            .Where(a => assetIds.Contains(a.AssetId))
            .Select(a => new { a.AssetId, a.IsCaseSensitive })
            .ToDictionaryAsync(a => a.AssetId, a => a.IsCaseSensitive);

        var requestedByAsset = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var transaction in transactionList)
        {
            var isCaseSensitive = assetPolicies.GetValueOrDefault(transaction.AssetId, true);
            if (!requestedByAsset.TryGetValue(transaction.AssetId, out var requested))
            {
                requested = new HashSet<string>(StringComparer.Ordinal);
                requestedByAsset[transaction.AssetId] = requested;
            }

            requested.Add(AddressComparison.Normalize(transaction.SourceAddress, isCaseSensitive));
            requested.Add(AddressComparison.Normalize(transaction.DestinationAddress, isCaseSensitive));
        }

        var addresses = await _context.Addresses
            .Include(a => a.Wallet)
            .ThenInclude(w => w.VaultAccount)
            .Where(a =>
                workspaceIdList.Contains(a.Wallet.VaultAccount.WorkspaceId) &&
                assetIds.Contains(a.Wallet.AssetId))
            .ToListAsync();

        var filteredAddresses = addresses
            .Where(address =>
            {
                var assetId = address.Wallet.AssetId;
                if (!requestedByAsset.TryGetValue(assetId, out var requested))
                {
                    return false;
                }

                var isCaseSensitive = assetPolicies.GetValueOrDefault(assetId, true);
                return requested.Contains(AddressComparison.Normalize(address.AddressValue, isCaseSensitive));
            })
            .ToList();

        return BuildAddressOwnershipLookup(filteredAddresses, assetPolicies);
    }

    public async Task<IReadOnlyDictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(
        string assetId,
        IEnumerable<string> addresses)
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

        var asset = await _context.Assets.FindAsync(assetId);
        var isCaseSensitive = asset?.IsCaseSensitive ?? true;
        var blockchainId = asset?.NativeAsset ?? assetId;
        var normalizedRequested = addressValues
            .Select(address => AddressComparison.Normalize(address, isCaseSensitive))
            .ToHashSet(StringComparer.Ordinal);

        var addressEntities = await _context.Addresses
            .Include(a => a.Wallet)
            .ThenInclude(w => w.VaultAccount)
            .Join(
                _context.Assets.Where(a =>
                    a.AssetId == blockchainId ||
                    a.NativeAsset == blockchainId),
                address => address.Wallet.AssetId,
                chainAsset => chainAsset.AssetId,
                (address, _) => address)
            .ToListAsync();

        var lookup = new Dictionary<string, AddressOwnership>();
        foreach (var addressEntity in addressEntities)
        {
            var wallet = addressEntity.Wallet;
            var vault = wallet?.VaultAccount;
            if (wallet == null || vault == null)
            {
                continue;
            }

            var normalizedAddress = AddressComparison.Normalize(addressEntity.AddressValue, isCaseSensitive);
            if (!normalizedRequested.Contains(normalizedAddress))
            {
                continue;
            }

            var key = BuildAddressKey(assetId, addressEntity.AddressValue);
            if (!lookup.ContainsKey(key))
            {
                lookup[key] = new AddressOwnership(vault.Id, vault.Name);
            }

            if (!isCaseSensitive)
            {
                var caseInsensitiveKey = BuildCaseInsensitiveAddressKey(assetId, addressEntity.AddressValue);
                if (!lookup.ContainsKey(caseInsensitiveKey))
                {
                    lookup[caseInsensitiveKey] = new AddressOwnership(vault.Id, vault.Name);
                }
            }
        }

        return lookup;
    }

    public AddressOwnership? ResolveOwnership(
        IReadOnlyDictionary<string, AddressOwnership> lookup,
        string assetId,
        string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        if (lookup.TryGetValue(BuildAddressKey(assetId, address), out var ownership))
        {
            return ownership;
        }

        return lookup.TryGetValue(BuildCaseInsensitiveAddressKey(assetId, address), out var caseInsensitiveOwnership)
            ? caseInsensitiveOwnership
            : null;
    }

    public TransactionDto MapToFireblocksDto(
        Transaction transaction,
        IReadOnlyDictionary<string, AddressOwnership> addressLookup,
        string? workspaceId)
    {
        var createdAtUnix = (decimal)transaction.CreatedAt.ToUnixTimeMilliseconds();
        var lastUpdatedUnix = (decimal)transaction.UpdatedAt.ToUnixTimeMilliseconds();
        var amountStr = transaction.Amount.ToString(CultureInfo.InvariantCulture);
        var networkFeeStr = transaction.NetworkFee.ToString(CultureInfo.InvariantCulture);
        var serviceFeeStr = transaction.ServiceFee.ToString(CultureInfo.InvariantCulture);
        // netAmount is what the recipient receives after any service fees
        // NetworkFee is paid to the network (miners/validators), not deducted from recipient
        // When TreatAsGrossAmount=true, the Amount already has fees deducted from the requested amount
        var netAmount = transaction.Amount - transaction.ServiceFee;
        var netAmountStr = netAmount.ToString(CultureInfo.InvariantCulture);
        var sourceOwnership = ResolveOwnership(addressLookup, transaction.AssetId, transaction.SourceAddress);
        var destinationOwnership = ResolveOwnership(addressLookup, transaction.AssetId, transaction.DestinationAddress);
        var sourceType = sourceOwnership != null ? TransferPeerType.VAULT_ACCOUNT : TransferPeerType.ONE_TIME_ADDRESS;
        var destinationType = destinationOwnership != null ? TransferPeerType.VAULT_ACCOUNT : TransferPeerType.ONE_TIME_ADDRESS;

        return new TransactionDto
        {
            Id = TransactionCompositeId.Build(workspaceId, transaction.Id),
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
            NetAmount = netAmountStr,
            AmountUSD = null,
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
                NetAmount = netAmountStr,
                AmountUSD = string.Empty,
            },
            Index = null,
            BlockchainIndex = string.Empty,
        };
    }

    public AdminTransactionDto MapToAdminDto(
        Transaction transaction,
        IReadOnlyDictionary<string, AddressOwnership> addressLookup,
        string? workspaceId)
    {
        var sourceOwnership = ResolveOwnership(addressLookup, transaction.AssetId, transaction.SourceAddress);
        var destinationOwnership = ResolveOwnership(addressLookup, transaction.AssetId, transaction.DestinationAddress);
        var sourceType = sourceOwnership != null ? AdminTransactionPartyType.INTERNAL : AdminTransactionPartyType.EXTERNAL;
        var destinationType = destinationOwnership != null ? AdminTransactionPartyType.INTERNAL : AdminTransactionPartyType.EXTERNAL;

        return new AdminTransactionDto
        {
            Id = TransactionCompositeId.Build(workspaceId, transaction.Id),
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
            FeeCurrency = transaction.FeeCurrency ?? transaction.AssetId,
            TreatAsGrossAmount = transaction.TreatAsGrossAmount,
            IsFrozen = transaction.IsFrozen,
            FailureReason = transaction.FailureReason,
            ReplacedByTxId = transaction.ReplacedByTxId == null
                ? null
                : TransactionCompositeId.Build(workspaceId, transaction.ReplacedByTxId),
            Confirmations = transaction.Confirmations,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt,
            InitiatedBy = transaction.InitiatedBy,
        };
    }

    private static IReadOnlyDictionary<string, AddressOwnership> BuildAddressOwnershipLookup(
        IEnumerable<Address> addresses,
        IReadOnlyDictionary<string, bool> assetPolicies)
    {
        var lookup = new Dictionary<string, AddressOwnership>();
        foreach (var address in addresses)
        {
            var wallet = address.Wallet;
            var vault = wallet?.VaultAccount;
            if (wallet == null || vault == null)
            {
                continue;
            }

            var isCaseSensitive = assetPolicies.GetValueOrDefault(wallet.AssetId, true);
            var key = BuildAddressKey(wallet.AssetId, address.AddressValue);
            if (!lookup.ContainsKey(key))
            {
                lookup[key] = new AddressOwnership(vault.Id, vault.Name);
            }

            if (!isCaseSensitive)
            {
                var caseInsensitiveKey = BuildCaseInsensitiveAddressKey(wallet.AssetId, address.AddressValue);
                if (!lookup.ContainsKey(caseInsensitiveKey))
                {
                    lookup[caseInsensitiveKey] = new AddressOwnership(vault.Id, vault.Name);
                }
            }
        }

        return lookup;
    }

    private static string BuildAddressKey(string assetId, string address)
    {
        return AddressComparison.BuildAssetAddressKey(assetId, address, isCaseSensitive: true);
    }

    private static string BuildCaseInsensitiveAddressKey(string assetId, string? address)
    {
        return AddressComparison.BuildAssetAddressKey(assetId, address, isCaseSensitive: false);
    }
}

public sealed record AddressOwnership(string VaultAccountId, string VaultAccountName);
