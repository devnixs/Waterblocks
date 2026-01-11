using FireblocksReplacement.Api.Infrastructure;
using FireblocksReplacement.Api.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace FireblocksReplacement.Api.Middleware;

public class FireblocksAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FireblocksAuthenticationMiddleware> _logger;

    public FireblocksAuthenticationMiddleware(RequestDelegate next, ILogger<FireblocksAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, FireblocksDbContext db, WorkspaceContext workspaceContext)
    {
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Skip authentication for health endpoint and Swagger
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.Contains("/health") || path.Contains("/swagger"))
        {
            await _next(context);
            return;
        }

        if (path.StartsWith("/supported_assets"))
        {
            await _next(context);
            return;
        }

        if (path.StartsWith("/admin"))
        {
            if (path.StartsWith("/admin/workspaces"))
            {
                await _next(context);
                return;
            }

            var workspaceHeader = context.Request.Headers["X-Workspace-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(workspaceHeader))
            {
                var workspaceExists = await db.Workspaces
                    .AsNoTracking()
                    .AnyAsync(w => w.Id == workspaceHeader);
                if (workspaceExists)
                {
                    workspaceContext.WorkspaceId = workspaceHeader;
                    await _next(context);
                    return;
                }
            }

            var defaultWorkspaceId = await db.Workspaces
                .AsNoTracking()
                .OrderBy(w => w.CreatedAt)
                .Select(w => w.Id)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(defaultWorkspaceId))
            {
                workspaceContext.WorkspaceId = defaultWorkspaceId;
            }

            await _next(context);
            return;
        }

        // For Fireblocks-compatible endpoints, check for API key header
        if (path.StartsWith("/vault") || path.StartsWith("/transactions"))
        {
            var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

            // For testing purposes, we accept any API key or Bearer token
            // In production, this would validate the JWT signature per Fireblocks spec
            if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(authHeader))
            {
                _logger.LogWarning("Request to {Path} without authentication", path);
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"message\":\"Unauthorized\",\"code\":401}");
                return;
            }

            if (!string.IsNullOrEmpty(apiKey))
            {
                var workspace = await db.ApiKeys
                    .AsNoTracking()
                    .Where(k => k.Key == apiKey)
                    .Select(k => new { k.WorkspaceId })
                    .FirstOrDefaultAsync();

                if (workspace == null)
                {
                    _logger.LogWarning("Request to {Path} with unknown API key", path);
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"message\":\"Unauthorized\",\"code\":401}");
                    return;
                }

                workspaceContext.WorkspaceId = workspace.WorkspaceId;
            }
            else
            {
                var defaultWorkspaceId = await db.Workspaces
                    .AsNoTracking()
                    .OrderBy(w => w.CreatedAt)
                    .Select(w => w.Id)
                    .FirstOrDefaultAsync();
                workspaceContext.WorkspaceId = defaultWorkspaceId;
            }

            _logger.LogDebug("Authenticated request to {Path}", path);
        }

        await _next(context);
    }
}
