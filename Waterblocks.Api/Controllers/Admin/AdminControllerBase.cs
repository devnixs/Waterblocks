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

    protected ActionResult<AdminResponse<T>> WorkspaceRequired<T>()
    {
        return BadRequest(AdminResponse<T>.Failure("Workspace is required", "WORKSPACE_REQUIRED"));
    }
}
