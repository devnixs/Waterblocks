using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Waterblocks.Api.Dtos.Fireblocks;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Middleware;

/// <summary>
/// Returns the first stored reply for a repeated Idempotency-Key on the supported routes.
/// </summary>
public sealed class IdempotencyMiddleware
{
    public const string HeaderName = "Idempotency-Key";

    private const int MaxKeyLength = 40;
    private static readonly HashSet<string> SupportedRoutes = new(StringComparer.Ordinal)
    {
        "/exchange_accounts",
        "/off_exchange/add",
        "/off_exchange/remove",
    };

    private readonly RequestDelegate _next;

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public static bool AppliesTo(HttpContext context)
    {
        return HttpMethods.IsPost(context.Request.Method)
            && SupportedRoutes.Contains(NormalizeRoute(context.Request.Path))
            && !string.IsNullOrEmpty(context.Request.Headers[HeaderName].FirstOrDefault());
    }

    public async Task InvokeAsync(
        HttpContext context,
        FireblocksDbContext database,
        WorkspaceContext workspace,
        IdempotencyContext idempotency)
    {
        var key = context.Request.Headers[HeaderName].First()!;
        if (key.Length > MaxKeyLength)
        {
            await WriteKeyTooLongAsync(context);
            return;
        }

        if (string.IsNullOrEmpty(workspace.WorkspaceId))
        {
            await _next(context);
            return;
        }

        var workspaceId = workspace.WorkspaceId;
        var route = NormalizeRoute(context.Request.Path);

        // The lock makes parallel calls with one key wait for the first reply.
        await using var lockConnection = new NpgsqlConnection(database.Database.GetConnectionString());
        await lockConnection.OpenAsync(context.RequestAborted);
        await using var lockTransaction = await lockConnection.BeginTransactionAsync(context.RequestAborted);
        await AcquireLockAsync(lockConnection, lockTransaction, workspaceId, route, key, context.RequestAborted);

        var record = await database.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.WorkspaceId == workspaceId && item.Route == route && item.Key == key,
                context.RequestAborted);

        if (record != null && record.ExpiresAt > DateTimeOffset.UtcNow)
        {
            await WriteStoredReplyAsync(context, record);
            return;
        }

        if (record != null)
        {
            await database.IdempotencyRecords
                .Where(item => item.Id == record.Id)
                .ExecuteDeleteAsync(context.RequestAborted);
        }

        idempotency.Begin(workspaceId, route, key);
        await RunAndStoreReplyAsync(context, database, idempotency, workspaceId, route, key);
    }

    private async Task RunAndStoreReplyAsync(
        HttpContext context,
        FireblocksDbContext database,
        IdempotencyContext idempotency,
        string workspaceId,
        string route,
        string key)
    {
        var originalBody = context.Response.Body;
        await using var replyBuffer = new MemoryStream();
        context.Response.Body = replyBuffer;
        try
        {
            await _next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        var replyBytes = replyBuffer.ToArray();
        if (!idempotency.ReplyStored && !context.RequestAborted.IsCancellationRequested)
        {
            var record = IdempotencyContext.CreateRecord(
                workspaceId,
                route,
                key,
                context.Response.StatusCode,
                context.Response.ContentType,
                Encoding.UTF8.GetString(replyBytes));
            await InsertRecordAsync(database, record);
        }

        await originalBody.WriteAsync(replyBytes, context.RequestAborted);
    }

    private static Task InsertRecordAsync(FireblocksDbContext database, IdempotencyRecord record)
    {
        return database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "IdempotencyRecords"
                ("WorkspaceId", "Route", "Key", "StatusCode", "ContentType", "Body", "CreatedAt", "ExpiresAt")
            VALUES ({record.WorkspaceId}, {record.Route}, {record.Key}, {record.StatusCode},
                {record.ContentType}, {record.Body}, {record.CreatedAt}, {record.ExpiresAt})
            ON CONFLICT ("WorkspaceId", "Route", "Key") DO NOTHING
            """);
    }

    private static async Task AcquireLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string workspaceId,
        string route,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@lockName, 0))";
        command.Parameters.AddWithValue("lockName", $"idempotency|{workspaceId}|{route}|{key}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WriteStoredReplyAsync(HttpContext context, IdempotencyRecord record)
    {
        context.Response.StatusCode = record.StatusCode;
        context.Response.ContentType = record.ContentType;
        await context.Response.WriteAsync(record.Body, context.RequestAborted);
    }

    private static async Task WriteKeyTooLongAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = IdempotencyContext.JsonContentType;
        var error = new ErrorResponse
        {
            Message = $"{HeaderName} must have at most {MaxKeyLength} characters",
            Code = StatusCodes.Status400BadRequest,
        };
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(error, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            context.RequestAborted);
    }

    private static string NormalizeRoute(PathString path)
    {
        return (path.Value ?? string.Empty).TrimEnd('/').ToLowerInvariant();
    }
}
