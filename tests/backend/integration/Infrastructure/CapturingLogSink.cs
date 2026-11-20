using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Waterblocks.IntegrationTests.Infrastructure;

/// <summary>
/// Keeps every log event of the API as text, so that tests can search the logs.
/// </summary>
public sealed class CapturingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<string> _messages = new();

    public void Emit(LogEvent logEvent)
    {
        var properties = string.Join(" ", logEvent.Properties.Select(property => $"{property.Key}={property.Value}"));
        _messages.Enqueue($"{logEvent.RenderMessage()} {properties} {logEvent.Exception}");
    }

    public string AllText => string.Join(Environment.NewLine, _messages);
}
