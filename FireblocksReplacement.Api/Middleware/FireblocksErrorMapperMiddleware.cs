using System.Net;
using System.Text.Json;
using FireblocksReplacement.Api.Dtos.Fireblocks;
using FireblocksReplacement.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FireblocksReplacement.Api.Middleware;

public class FireblocksErrorMapperMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FireblocksErrorMapperMiddleware> _logger;

    public FireblocksErrorMapperMiddleware(RequestDelegate next, ILogger<FireblocksErrorMapperMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        var (statusCode, errorCode, message) = MapExceptionToFireblocksError(exception);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            Message = message,
            Code = errorCode,
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        var json = JsonSerializer.Serialize(errorResponse, options);
        await context.Response.WriteAsync(json);
    }

    private (HttpStatusCode statusCode, decimal errorCode, string message) MapExceptionToFireblocksError(Exception exception)
    {
        if (exception is DbUpdateException updateException && IsExternalTxIdUniqueViolation(updateException))
        {
            return (HttpStatusCode.Conflict, 1438, "External transaction id already exists");
        }

        return exception switch
        {
            DuplicateExternalTxIdException dup => (HttpStatusCode.Conflict, 1438, dup.Message),
            ArgumentNullException => (HttpStatusCode.BadRequest, 400, exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, 400, exception.Message),
            InvalidOperationException => (HttpStatusCode.BadRequest, 400, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, 401, "Unauthorized"),
            KeyNotFoundException => (HttpStatusCode.NotFound, 404, "Resource not found"),
            _ => (HttpStatusCode.InternalServerError, 500, "An internal error occurred"),
        };
    }

    private static bool IsExternalTxIdUniqueViolation(DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException postgresException)
        {
            return postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                && postgresException.ConstraintName == "IX_Transactions_ExternalTxId";
        }

        return false;
    }
}
