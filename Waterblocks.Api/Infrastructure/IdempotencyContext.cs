using System.Text.Json;
using System.Text.Json.Serialization;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Infrastructure;

/// <summary>
/// The Idempotency-Key state of one request. A service can store the reply in its own database transaction.
/// </summary>
public sealed class IdempotencyContext
{
    public const string JsonContentType = "application/json; charset=utf-8";

    private static readonly TimeSpan RecordLifetime = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions ReplyOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private string? _workspaceId;
    private string? _route;
    private string? _key;

    public bool ReplyStored { get; private set; }

    public void Begin(string workspaceId, string route, string key)
    {
        _workspaceId = workspaceId;
        _route = route;
        _key = key;
    }

    public void StageReply(FireblocksDbContext database, int statusCode, object reply)
    {
        if (_key == null)
        {
            return;
        }

        database.IdempotencyRecords.Add(CreateRecord(
            _workspaceId!,
            _route!,
            _key,
            statusCode,
            JsonContentType,
            JsonSerializer.Serialize(reply, reply.GetType(), ReplyOptions)));
    }

    public void MarkReplyStored()
    {
        ReplyStored = _key != null;
    }

    public static IdempotencyRecord CreateRecord(
        string workspaceId,
        string route,
        string key,
        int statusCode,
        string? contentType,
        string body)
    {
        var now = DateTimeOffset.UtcNow;
        return new IdempotencyRecord
        {
            WorkspaceId = workspaceId,
            Route = route,
            Key = key,
            StatusCode = statusCode,
            ContentType = contentType,
            Body = body,
            CreatedAt = now,
            ExpiresAt = now.Add(RecordLifetime),
        };
    }
}
