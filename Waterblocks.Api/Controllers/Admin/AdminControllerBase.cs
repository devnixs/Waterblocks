using Microsoft.AspNetCore.Mvc;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Infrastructure;

namespace Waterblocks.Api.Controllers.Admin;

public abstract class AdminControllerBase : ControllerBase
{
    protected AdminControllerBase(WorkspaceContext workspace)
    {
        Workspace = workspace;
    }

    protected WorkspaceContext Workspace { get; }

    protected bool TryGetWorkspaceId<T>(out string workspaceId, out ActionResult<AdminResponse<T>> failure)
    {
        workspaceId = Workspace.WorkspaceId ?? string.Empty;
        if (string.IsNullOrEmpty(workspaceId))
        {
            failure = WorkspaceRequired<T>();
            return false;
        }

        failure = default!;
        return true;
    }

    protected ActionResult<AdminResponse<T>> WorkspaceRequired<T>()
    {
        return BadRequest(AdminResponse<T>.Failure("Workspace is required", "WORKSPACE_REQUIRED"));
    }
}
