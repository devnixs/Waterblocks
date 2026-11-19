namespace Waterblocks.Api.Services;

/// <summary>
/// Sends pending mirror events to the Gate simulator about once each second.
/// </summary>
public sealed class ExchangeMirrorWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExchangeMirrorWorker> _logger;

    public ExchangeMirrorWorker(IServiceScopeFactory scopeFactory, ILogger<ExchangeMirrorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IExchangeMirrorProcessor>();
                await processor.ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Exchange mirror loop failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
