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
        await using var responseBodyBuffer = new MemoryStream();
        context.Response.Body = responseBodyBuffer;

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

            string responseBody;
            try
            {
                responseBodyBuffer.Position = 0;
                responseBody = await ReadBodyAsync(responseBodyBuffer, context.Response.ContentType);
                responseBodyBuffer.Position = 0;
            }
            finally
            {
                context.Response.Body = originalResponseBody;
                responseBodyBuffer.Position = 0;
                await responseBodyBuffer.CopyToAsync(originalResponseBody);
            }

            LogHttpExchange(
                context,
                stopwatch.ElapsedMilliseconds,
                requestHeaders,
                requestBody,
                ToLoggableHeaders(context.Response.Headers),
                responseBody,
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

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var content = await reader.ReadToEndAsync();
        if (string.IsNullOrEmpty(content))
        {
            return "[empty]";
        }

        if (content.Length <= MaxBodyLength)
        {
            return content;
        }

        return $"{content[..MaxBodyLength]}...[truncated]";
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
}
