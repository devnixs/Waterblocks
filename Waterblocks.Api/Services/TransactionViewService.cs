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
    IQueryable<Transaction> ApplyWorkspaceAddressFilter(IQueryable<Transaction> query, HashSet<string> addresses);
    Task<IReadOnlyDictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(IEnumerable<Transaction> transactions, string workspaceId);
    Task<IReadOnlyDictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(IEnumerable<Transaction> transactions, IEnumerable<string> workspaceIds);
    Task<IReadOnlyDictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(string assetId, IEnumerable<string> addresses, string workspaceId);
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

    public IQueryable<Transaction> ApplyWorkspaceAddressFilter(IQueryable<Transaction> query, HashSet<string> addresses)
    {
        return query.Where(t => addresses.Contains(t.SourceAddress) || addresses.Contains(t.DestinationAddress));
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

        var workspaceIdList = workspaceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        if (workspaceIdList.Count == 0)
        {
            return new Dictionary<string, AddressOwnership>();
        }

        var addresses = await _context.Addresses
            .Include(a => a.Wallet)
            .ThenInclude(w => w.VaultAccount)
            .Where(a => addressValues.Contains(a.AddressValue)
                        && workspaceIdList.Contains(a.Wallet.VaultAccount.WorkspaceId))
            .ToListAsync();

        return BuildAddressOwnershipLookup(addresses);
    }

    public async Task<IReadOnlyDictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(
        string assetId,
        IEnumerable<string> addresses,
        string workspaceId)
    {
        var addressValues = addresses
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address.Trim())
            .Distinct()
            .ToList();

        if (addressValues.Count == 0 || string.IsNullOrWhiteSpace(workspaceId))
        {
            return new Dictionary<string, AddressOwnership>();
        }

        var addressEntities = await _context.Addresses
            .Include(a => a.Wallet)
            .ThenInclude(w => w.VaultAccount)
            .Where(a => addressValues.Contains(a.AddressValue)
                        && a.Wallet.AssetId == assetId
                        && a.Wallet.VaultAccount.WorkspaceId == workspaceId)
            .ToListAsync();

        return BuildAddressOwnershipLookup(addressEntities);
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

        return lookup.TryGetValue(BuildAddressKey(assetId, address), out var ownership)
            ? ownership
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
            NetAmount = (transaction.Amount - transaction.NetworkFee - transaction.ServiceFee).ToString(CultureInfo.InvariantCulture),
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
                NetAmount = (transaction.Amount - transaction.NetworkFee - transaction.ServiceFee).ToString(CultureInfo.InvariantCulture),
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
            IsFrozen = transaction.IsFrozen,
            FailureReason = transaction.FailureReason,
            ReplacedByTxId = transaction.ReplacedByTxId == null
                ? null
                : TransactionCompositeId.Build(workspaceId, transaction.ReplacedByTxId),
            Confirmations = transaction.Confirmations,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt,
        };
    }

    private static IReadOnlyDictionary<string, AddressOwnership> BuildAddressOwnershipLookup(IEnumerable<Address> addresses)
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

            var key = BuildAddressKey(wallet.AssetId, address.AddressValue);
            if (!lookup.ContainsKey(key))
            {
                lookup[key] = new AddressOwnership(vault.Id, vault.Name);
            }
        }

        return lookup;
    }

    private static string BuildAddressKey(string assetId, string address)
    {
        return $"{assetId}|{address}";
    }
}

public sealed record AddressOwnership(string VaultAccountId, string VaultAccountName);
