using Microsoft.AspNetCore.Http;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Infrastructure;

namespace Waterblocks.Api.Services;

public abstract class AdminServiceBase
{
    protected AdminServiceBase(WorkspaceContext workspace)
    {
        Workspace = workspace;
    }

    protected WorkspaceContext Workspace { get; }

    protected bool TryGetWorkspaceId<T>(out string workspaceId, out AdminServiceResult<T> failure)
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

    protected static AdminServiceResult<T> Success<T>(T data)
    {
        return new AdminServiceResult<T>(AdminResponse<T>.Success(data), StatusCodes.Status200OK);
    }

    protected static AdminServiceResult<T> Failure<T>(string message, string code)
    {
        return new AdminServiceResult<T>(AdminResponse<T>.Failure(message, code), StatusCodes.Status400BadRequest);
    }

    protected static AdminServiceResult<T> NotFound<T>(string message, string code)
    {
        return new AdminServiceResult<T>(AdminResponse<T>.Failure(message, code), StatusCodes.Status404NotFound);
    }

    protected static AdminServiceResult<T> WorkspaceRequired<T>()
    {
        return new AdminServiceResult<T>(AdminResponse<T>.Failure("Workspace is required", "WORKSPACE_REQUIRED"), StatusCodes.Status400BadRequest);
    }
}
