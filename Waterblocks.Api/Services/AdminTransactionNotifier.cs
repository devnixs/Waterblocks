using Microsoft.AspNetCore.SignalR;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Hubs;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public interface IAdminTransactionNotifier
{
    Task<AdminTransactionDto> NotifyUpsertAsync(Transaction transaction, string workspaceId);
    Task NotifyListsUpdatedAsync(string workspaceId);
}

public sealed class AdminTransactionNotifier : IAdminTransactionNotifier
{
    private readonly IHubContext<AdminHub> _hub;
    private readonly IAdminTransactionMapper _mapper;

    public AdminTransactionNotifier(IHubContext<AdminHub> hub, IAdminTransactionMapper mapper)
    {
        _hub = hub;
        _mapper = mapper;
    }

    public async Task<AdminTransactionDto> NotifyUpsertAsync(Transaction transaction, string workspaceId)
    {
        var dto = await _mapper.MapAsync(transaction, workspaceId);
        await _hub.Clients.Group(workspaceId).SendAsync("transactionUpserted", dto);
        await NotifyListsUpdatedAsync(workspaceId);
        return dto;
    }

    public async Task NotifyListsUpdatedAsync(string workspaceId)
    {
        await _hub.Clients.Group(workspaceId).SendAsync("transactionsUpdated");
        await _hub.Clients.Group(workspaceId).SendAsync("vaultsUpdated");
    }
}
