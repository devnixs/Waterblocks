using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace Waterblocks.Api.Middleware;

public class HttpTrafficLoggingMiddleware
{
    private const int MaxBodyLength = 32 * 1024;
    private static readonly HashSet<string> RedactedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "X-API-Key",
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<HttpTrafficLoggingMiddleware> _logger;

    public HttpTrafficLoggingMiddleware(RequestDelegate next, ILogger<HttpTrafficLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestHeaders = ToLoggableHeaders(context.Request.Headers);
        var requestBody = await ReadRequestBodyAsync(context.Request);
        var stopwatch = Stopwatch.StartNew();
        Exception? unhandledException = null;

        if (IsWebSocketUpgrade(context.Request))
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                unhandledException = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                LogHttpExchange(
                    context,
                    stopwatch.ElapsedMilliseconds,
                    requestHeaders,
                    requestBody,
                    responseHeaders: null,
                    responseBody: "[websocket upgrade]",
                    unhandledException);
            }

            return;
        }

        var originalResponseBody = context.Response.Body;
        await using var responseCapture = new CappedCaptureStream(originalResponseBody, MaxBodyLength);
        context.Response.Body = responseCapture;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            unhandledException = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            context.Response.Body = originalResponseBody;

            LogHttpExchange(
                context,
                stopwatch.ElapsedMilliseconds,
                requestHeaders,
                requestBody,
                ToLoggableHeaders(context.Response.Headers),
                FormatCapturedBody(responseCapture, context.Response.ContentType),
                unhandledException);
        }
    }

    private void LogHttpExchange(
        HttpContext context,
        long elapsedMilliseconds,
        IDictionary<string, string> requestHeaders,
        string requestBody,
        IDictionary<string, string>? responseHeaders,
        string responseBody,
        Exception? unhandledException)
    {
        var level = unhandledException is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError
            ? LogLevel.Error
            : context.Response.StatusCode >= StatusCodes.Status400BadRequest
                ? LogLevel.Warning
                : LogLevel.Information;

        _logger.Log(
            level,
            unhandledException,
            "HTTP {Method} {Path}{QueryString} responded {StatusCode} in {ElapsedMilliseconds} ms. TraceId={TraceId} RequestHeaders={@RequestHeaders} RequestBody={RequestBody} ResponseHeaders={@ResponseHeaders} ResponseBody={ResponseBody}",
            context.Request.Method,
            context.Request.Path.Value,
            context.Request.QueryString.Value,
            context.Response.StatusCode,
            elapsedMilliseconds,
            context.TraceIdentifier,
            requestHeaders,
            requestBody,
            responseHeaders,
            responseBody);
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (!CanHaveBody(request.ContentLength, request.Headers))
        {
            return "[empty]";
        }

        request.EnableBuffering();
        try
        {
            return await ReadBodyAsync(request.Body, request.ContentType);
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static async Task<string> ReadBodyAsync(Stream stream, string? contentType)
    {
        if (!IsTextBasedContentType(contentType))
        {
            return $"[body omitted for content type: {contentType ?? "unknown"}]";
        }

        if (stream.CanSeek && stream.Length == 0)
        {
            return "[empty]";
        }

        // Read at most MaxBodyLength + 1 so large bodies are never fully buffered in memory.
        var buffer = new char[MaxBodyLength + 1];
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
        if (read == 0)
        {
            return "[empty]";
        }

        if (read <= MaxBodyLength)
        {
            return new string(buffer, 0, read);
        }

        return $"{new string(buffer, 0, MaxBodyLength)}...[truncated]";
    }

    private static string FormatCapturedBody(CappedCaptureStream capture, string? contentType)
    {
        if (!IsTextBasedContentType(contentType))
        {
            return $"[body omitted for content type: {contentType ?? "unknown"}]";
        }

        if (capture.CapturedLength == 0)
        {
            return "[empty]";
        }

        var content = Encoding.UTF8.GetString(capture.CapturedBytes, 0, capture.CapturedLength);
        return capture.Truncated
            ? $"{content}...[truncated]"
            : content;
    }

    private static bool CanHaveBody(long? contentLength, IHeaderDictionary headers)
    {
        if (contentLength.GetValueOrDefault() > 0)
        {
            return true;
        }

        return headers.ContainsKey("Transfer-Encoding");
    }

    private static bool IsTextBasedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return true;
        }

        return contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("application/problem+json", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWebSocketUpgrade(HttpRequest request)
    {
        return request.Headers.TryGetValue("Upgrade", out var upgradeValues)
            && upgradeValues.Any(value => value is not null && value.Equals("websocket", StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string> ToLoggableHeaders(IHeaderDictionary headers)
    {
        var loggableHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            loggableHeaders[header.Key] = RedactedHeaders.Contains(header.Key)
                ? "[redacted]"
                : JoinValues(header.Value);
        }

        return loggableHeaders;
    }

    private static string JoinValues(StringValues values)
    {
        return string.Join(", ", values.ToArray());
    }

    /// <summary>
    /// Pass-through response stream that keeps only the first <see cref="MaxBodyLength"/> bytes for logging.
    /// </summary>
    private sealed class CappedCaptureStream : Stream
    {
        private readonly Stream _inner;
        private readonly byte[] _capture;
        private readonly int _maxCaptureBytes;
        private int _capturedLength;
        private bool _truncated;

        public CappedCaptureStream(Stream inner, int maxCaptureBytes)
        {
            _inner = inner;
            _maxCaptureBytes = maxCaptureBytes;
            _capture = new byte[maxCaptureBytes];
        }

        public byte[] CapturedBytes => _capture;
        public int CapturedLength => _capturedLength;
        public bool Truncated => _truncated;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            Capture(buffer.AsSpan(offset, count));
            _inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Capture(buffer);
            _inner.Write(buffer);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Capture(buffer.AsSpan(offset, count));
            await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Capture(buffer.Span);
            await _inner.WriteAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            // Do not dispose the inner response body stream; ASP.NET owns it.
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private void Capture(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                return;
            }

            var remaining = _maxCaptureBytes - _capturedLength;
            if (remaining > 0)
            {
                var toCopy = Math.Min(remaining, buffer.Length);
                buffer[..toCopy].CopyTo(_capture.AsSpan(_capturedLength));
                _capturedLength += toCopy;
                buffer = buffer[toCopy..];
            }

            if (!buffer.IsEmpty)
            {
                _truncated = true;
            }
        }
    }
}
