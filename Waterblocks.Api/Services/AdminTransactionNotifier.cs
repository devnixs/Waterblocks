using Microsoft.AspNetCore.SignalR;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Hubs;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public interface IAdminTransactionNotifier
{
    Task<AdminTransactionDto> NotifyUpsertAsync(Transaction transaction, string workspaceId);
    Task NotifyListsUpdatedAsync(IEnumerable<string> workspaceIds);
}

public sealed class AdminTransactionNotifier : IAdminTransactionNotifier
{
    private readonly IHubContext<AdminHub> _hub;
    private readonly IAdminTransactionMapper _mapper;
    private readonly ITransactionViewService _transactionView;

    public AdminTransactionNotifier(
        IHubContext<AdminHub> hub,
        IAdminTransactionMapper mapper,
        ITransactionViewService transactionView)
    {
        _hub = hub;
        _mapper = mapper;
        _transactionView = transactionView;
    }

    public async Task<AdminTransactionDto> NotifyUpsertAsync(Transaction transaction, string workspaceId)
    {
        var workspaceIds = await GetRecipientWorkspaceIdsAsync(transaction, workspaceId);
        AdminTransactionDto? responseDto = null;

        foreach (var recipientWorkspaceId in workspaceIds)
        {
            var dto = await _mapper.MapAsync(transaction, recipientWorkspaceId);
            await _hub.Clients.Group(recipientWorkspaceId).SendAsync("transactionUpserted", dto);

            if (recipientWorkspaceId == workspaceId)
            {
                responseDto = dto;
            }
        }

        await NotifyListsUpdatedAsync(workspaceIds);
        return responseDto ?? await _mapper.MapAsync(transaction, workspaceId);
    }

    public async Task NotifyListsUpdatedAsync(IEnumerable<string> workspaceIds)
    {
        foreach (var workspaceId in workspaceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal))
        {
            await _hub.Clients.Group(workspaceId).SendAsync("transactionsUpdated");
            await _hub.Clients.Group(workspaceId).SendAsync("vaultsUpdated");
        }

        await _hub.Clients.Group(AdminHub.PendingTransactionsGroup).SendAsync("pendingTransactionsUpdated");
    }

    private async Task<IReadOnlyList<string>> GetRecipientWorkspaceIdsAsync(Transaction transaction, string workspaceId)
    {
        var recipients = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            recipients.Add(workspaceId);
        }

        var addressLookup = await _transactionView.BuildAddressOwnershipLookupAsync(
            transaction.AssetId,
            new[] { transaction.SourceAddress ?? string.Empty, transaction.DestinationAddress ?? string.Empty });

        var sourceOwnership = _transactionView.ResolveOwnership(addressLookup, transaction.AssetId, transaction.SourceAddress);
        var destinationOwnership = _transactionView.ResolveOwnership(addressLookup, transaction.AssetId, transaction.DestinationAddress);

        if (!string.IsNullOrWhiteSpace(sourceOwnership?.WorkspaceId))
        {
            recipients.Add(sourceOwnership.WorkspaceId);
        }

        if (!string.IsNullOrWhiteSpace(destinationOwnership?.WorkspaceId))
        {
            recipients.Add(destinationOwnership.WorkspaceId);
        }

        return recipients.ToList();
    }
}
