using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FireblocksReplacement.Api.Infrastructure.Db;
using FireblocksReplacement.Api.Dtos.Admin;
using FireblocksReplacement.Api.Models;

namespace FireblocksReplacement.Api.Controllers.Admin;

[ApiController]
[Route("admin/workspaces")]
public class AdminWorkspacesController : ControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<AdminWorkspacesController> _logger;

    public AdminWorkspacesController(FireblocksDbContext context, ILogger<AdminWorkspacesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<AdminResponse<List<AdminWorkspaceDto>>>> GetWorkspaces()
    {
        var workspaces = await _context.Workspaces
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
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Default",
            Key = Guid.NewGuid().ToString("N"),
            WorkspaceId = workspace.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Workspaces.Add(workspace);
        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created workspace {WorkspaceId} ({Name})", workspace.Id, workspace.Name);

        workspace.ApiKeys.Add(apiKey);
        return Ok(AdminResponse<AdminWorkspaceDto>.Success(MapToDto(workspace)));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<AdminResponse<bool>>> DeleteWorkspace(string id)
    {
        var workspace = await _context.Workspaces
            .Include(w => w.ApiKeys)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workspace == null)
        {
            return NotFound(AdminResponse<bool>.Failure("Workspace not found", "WORKSPACE_NOT_FOUND"));
        }

        _context.Workspaces.Remove(workspace);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted workspace {WorkspaceId}", id);

        return Ok(AdminResponse<bool>.Success(true));
    }

    private static AdminWorkspaceDto MapToDto(Workspace workspace)
    {
        return new AdminWorkspaceDto
        {
            Id = workspace.Id,
            Name = workspace.Name,
            ApiKeys = workspace.ApiKeys.Select(k => new AdminApiKeyDto
            {
                Id = k.Id,
                Name = k.Name,
                Key = k.Key,
                CreatedAt = k.CreatedAt
            }).ToList(),
            CreatedAt = workspace.CreatedAt,
            UpdatedAt = workspace.UpdatedAt
        };
    }
}
