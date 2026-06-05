using Microsoft.AspNetCore.SignalR;

namespace Waterblocks.Api.Hubs;

public class AdminHub : Hub
{
    public const string PendingTransactionsGroup = "pending-transactions";
    public const string WorkspacesGroup = "workspaces";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, PendingTransactionsGroup);
        await Groups.AddToGroupAsync(Context.ConnectionId, WorkspacesGroup);

        var workspaceId = Context.GetHttpContext()?.Request.Query["workspaceId"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, workspaceId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, PendingTransactionsGroup);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, WorkspacesGroup);

        var workspaceId = Context.GetHttpContext()?.Request.Query["workspaceId"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, workspaceId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
