using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Infrastructure;

namespace Waterblocks.Api.Controllers.Admin;

[ApiController]
[Route("admin/settings")]
public class AdminSettingsController : AdminControllerBase
{
    private readonly FireblocksDbContext _context;

    public AdminSettingsController(FireblocksDbContext context, WorkspaceContext workspace)
        : base(workspace)
    {
        _context = context;
    }

    [HttpGet("auto-transitions")]
    public async Task<ActionResult<AdminResponse<AdminAutoTransitionSettingsDto>>> GetAutoTransitions()
    {
        if (!TryGetWorkspaceId<AdminAutoTransitionSettingsDto>(out var workspaceId, out var failure))
        {
            return failure;
        }

        var enabled = await _context.Workspaces
            .Where(w => !w.IsDeleted && w.Id == workspaceId)
            .Select(w => (bool?)w.AutoTransitionEnabled)
            .FirstOrDefaultAsync();

        if (enabled == null)
        {
            return NotFound(AdminResponse<AdminAutoTransitionSettingsDto>.Failure("Workspace not found", "WORKSPACE_NOT_FOUND"));
        }

        return Ok(AdminResponse<AdminAutoTransitionSettingsDto>.Success(new AdminAutoTransitionSettingsDto
        {
            Enabled = enabled.Value,
        }));
    }

    [HttpPost("auto-transitions")]
    public async Task<ActionResult<AdminResponse<AdminAutoTransitionSettingsDto>>> SetAutoTransitions(
        [FromBody] AdminAutoTransitionSettingsDto request)
    {
        if (!TryGetWorkspaceId<AdminAutoTransitionSettingsDto>(out var workspaceId, out var failure))
        {
            return failure;
        }

        var workspace = await _context.Workspaces
            .Where(w => !w.IsDeleted)
            .FirstOrDefaultAsync(w => w.Id == workspaceId);
        if (workspace == null)
        {
            return NotFound(AdminResponse<AdminAutoTransitionSettingsDto>.Failure("Workspace not found", "WORKSPACE_NOT_FOUND"));
        }

        workspace.AutoTransitionEnabled = request.Enabled;
        workspace.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(AdminResponse<AdminAutoTransitionSettingsDto>.Success(new AdminAutoTransitionSettingsDto
        {
            Enabled = request.Enabled,
        }));
    }
}
