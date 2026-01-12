using Microsoft.AspNetCore.SignalR;

namespace Waterblocks.Api.Hubs;

public class AdminHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var workspaceId = Context.GetHttpContext()?.Request.Query["workspaceId"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, workspaceId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var workspaceId = Context.GetHttpContext()?.Request.Query["workspaceId"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, workspaceId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
