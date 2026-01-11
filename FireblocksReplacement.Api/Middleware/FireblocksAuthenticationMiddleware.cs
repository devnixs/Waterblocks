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

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for health endpoint and Swagger
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.Contains("/health") || path.Contains("/swagger") || path.Contains("/admin"))
        {
            await _next(context);
            return;
        }

        // For Fireblocks-compatible endpoints, check for API key header
        if (path.StartsWith("/vault") || path.StartsWith("/transactions") || path.StartsWith("/supported_assets"))
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

            _logger.LogDebug("Authenticated request to {Path}", path);
        }

        await _next(context);
    }
}
