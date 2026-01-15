using Microsoft.AspNetCore.SignalR;
using Waterblocks.Api.Hubs;

namespace Waterblocks.Api.Services;

public interface IRealtimeNotifier
{
    Task NotifyTransactionsUpdatedAsync(string? workspaceId);
    Task NotifyVaultsUpdatedAsync(string? workspaceId);
}

public sealed class RealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<AdminHub> _hub;

    public RealtimeNotifier(IHubContext<AdminHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyTransactionsUpdatedAsync(string? workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return Task.CompletedTask;
        }

        return _hub.Clients.Group(workspaceId).SendAsync("transactionsUpdated");
    }

    public Task NotifyVaultsUpdatedAsync(string? workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return Task.CompletedTask;
        }

        return _hub.Clients.Group(workspaceId).SendAsync("vaultsUpdated");
    }
}
