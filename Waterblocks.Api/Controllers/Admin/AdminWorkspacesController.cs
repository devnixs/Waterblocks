using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Hubs;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Controllers.Admin;

[ApiController]
[Route("admin/workspaces")]
public class AdminWorkspacesController : AdminControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminWorkspacesController> _logger;
    private readonly IHubContext<AdminHub> _hub;

    public AdminWorkspacesController(
        FireblocksDbContext context,
        IConfiguration configuration,
        ILogger<AdminWorkspacesController> logger,
        IHubContext<AdminHub> hub,
        WorkspaceContext workspace)
        : base(workspace)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _hub = hub;
    }

    [HttpGet]
    public async Task<ActionResult<AdminResponse<List<AdminWorkspaceDto>>>> GetWorkspaces()
    {
        var workspaces = await _context.Workspaces
            .Where(w => !w.IsDeleted)
            .Include(w => w.ApiKeys)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync();

        var dtos = workspaces.Select(MapToDto).ToList();
        return Ok(AdminResponse<List<AdminWorkspaceDto>>.Success(dtos));
    }

    [HttpPost]
    public async Task<ActionResult<AdminResponse<AdminWorkspaceDto>>> CreateWorkspace(
        [FromBody] CreateWorkspaceRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(AdminResponse<AdminWorkspaceDto>.Failure("Workspace name is required", "NAME_REQUIRED"));
        }

        var workspace = new Workspace
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name.Trim(),
            AutoTransitionEnabled = request.AutoTransitionEnabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Default",
            Key = Guid.NewGuid().ToString("N"),
            WorkspaceId = workspace.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _context.Workspaces.Add(workspace);
        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created workspace {WorkspaceId} ({Name}) with auto-transition {AutoTransitionEnabled}",
            workspace.Id,
            workspace.Name,
            request.AutoTransitionEnabled);

        workspace.ApiKeys.Add(apiKey);
        await NotifyWorkspacesUpdatedAsync();
        return Ok(AdminResponse<AdminWorkspaceDto>.Success(MapToDto(workspace)));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<AdminResponse<bool>>> DeleteWorkspace(string id)
    {
        var workspace = await _context.Workspaces
            .Where(w => !w.IsDeleted)
            .Include(w => w.ApiKeys)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workspace == null)
        {
            return NotFound(AdminResponse<bool>.Failure("Workspace not found", "WORKSPACE_NOT_FOUND"));
        }

        SoftDeleteWorkspace(workspace, DateTimeOffset.UtcNow);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Soft-deleted workspace {WorkspaceId}", id);

        await NotifyWorkspacesUpdatedAsync();
        return Ok(AdminResponse<bool>.Success(true));
    }

    [HttpPost("archive-all")]
    public async Task<ActionResult<AdminResponse<bool>>> ArchiveAllWorkspaces()
    {
        if (!_configuration.GetValue<bool>("ARCHIVE_ALL_WORKSPACES_ENABLED"))
        {
            return BadRequest(AdminResponse<bool>.Failure(
                "Archive all workspaces is disabled",
                "FEATURE_DISABLED"));
        }

        var archivedAt = DateTimeOffset.UtcNow;
        var workspaces = await _context.Workspaces
            .Where(w => !w.IsDeleted && w.Name != "Default")
            .ToListAsync();

        foreach (var workspace in workspaces)
        {
            SoftDeleteWorkspace(workspace, archivedAt);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Bulk soft-deleted {WorkspaceCount} workspaces", workspaces.Count);

        if (workspaces.Count > 0)
        {
            await NotifyWorkspacesUpdatedAsync();
        }

        return Ok(AdminResponse<bool>.Success(true));
    }

    private Task NotifyWorkspacesUpdatedAsync()
    {
        return _hub.Clients.Group(AdminHub.WorkspacesGroup).SendAsync("workspacesUpdated");
    }

    private static void SoftDeleteWorkspace(Workspace workspace, DateTimeOffset deletedAt)
    {
        workspace.IsDeleted = true;
        workspace.DeletedAt = deletedAt;
        workspace.UpdatedAt = deletedAt;
    }

    private static AdminWorkspaceDto MapToDto(Workspace workspace)
    {
        return new AdminWorkspaceDto
        {
            Id = workspace.Id,
            Name = workspace.Name,
            AutoTransitionEnabled = workspace.AutoTransitionEnabled,
            ApiKeys = workspace.ApiKeys.Select(k => new AdminApiKeyDto
            {
                Id = k.Id,
                Name = k.Name,
                Key = k.Key,
                CreatedAt = k.CreatedAt,
            }).ToList(),
            CreatedAt = workspace.CreatedAt,
            UpdatedAt = workspace.UpdatedAt,
        };
    }
}
