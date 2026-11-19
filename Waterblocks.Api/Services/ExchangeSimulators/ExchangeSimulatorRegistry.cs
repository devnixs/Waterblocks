using Waterblocks.Api.Infrastructure;

namespace Waterblocks.Api.Services.ExchangeSimulators;

/// <summary>
/// Finds the exchange simulator client of an exchange type.
/// </summary>
public interface IExchangeSimulatorRegistry
{
    /// <summary>
    /// All registered clients, ordered by display name.
    /// </summary>
    IReadOnlyList<IExchangeSimulatorClient> All { get; }

    /// <summary>
    /// Returns the client of an exchange type, or null when no simulator serves that type.
    /// </summary>
    IExchangeSimulatorClient? Find(string exchangeType);

    /// <summary>
    /// Returns the client of an exchange type, or refuses the request when no simulator serves that type.
    /// </summary>
    IExchangeSimulatorClient Require(string exchangeType);
}

public sealed class ExchangeSimulatorRegistry : IExchangeSimulatorRegistry
{
    private readonly Dictionary<string, IExchangeSimulatorClient> _clients;

    public ExchangeSimulatorRegistry(IEnumerable<IExchangeSimulatorClient> clients)
    {
        All = clients
            .OrderBy(client => client.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(client => client.ExchangeType, StringComparer.Ordinal)
            .ToList();
        _clients = new Dictionary<string, IExchangeSimulatorClient>(StringComparer.Ordinal);
        foreach (var client in All)
        {
            if (!_clients.TryAdd(client.ExchangeType, client))
            {
                throw new InvalidOperationException($"Exchange type {client.ExchangeType} has more than one exchange simulator client");
            }
        }
    }

    public IReadOnlyList<IExchangeSimulatorClient> All { get; }

    public IExchangeSimulatorClient? Find(string exchangeType)
    {
        return _clients.GetValueOrDefault(exchangeType);
    }

    public IExchangeSimulatorClient Require(string exchangeType)
    {
        return Find(exchangeType)
            ?? throw ExchangeOperationException.BadRequest(
                "EXCHANGE_TYPE_NOT_SUPPORTED",
                $"Exchange type {exchangeType} has no exchange simulator");
    }
}

public static class ExchangeSimulatorServiceCollectionExtensions
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Registers the client of one exchange simulator as a typed HTTP client.
    /// </summary>
    public static IServiceCollection AddExchangeSimulatorClient<TClient>(this IServiceCollection services)
        where TClient : class, IExchangeSimulatorClient
    {
        services.AddHttpClient<TClient>(client => client.Timeout = RequestTimeout);
        services.AddTransient<IExchangeSimulatorClient>(provider => provider.GetRequiredService<TClient>());
        return services;
    }
}
