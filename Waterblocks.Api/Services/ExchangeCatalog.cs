using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

/// <summary>
/// Reads and changes the exchange catalog. Every exchange fact that the code needs comes from here.
/// </summary>
public interface IExchangeCatalog
{
    Task<Exchange?> FindAsync(string exchangeType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the exchange of a type, or refuses the request when the catalog does not have it.
    /// </summary>
    Task<Exchange> RequireAsync(string? exchangeType);

    /// <summary>
    /// Returns the exchange of a type, or refuses the request when the exchange has no simulator URL.
    /// </summary>
    Task<Exchange> RequireConnectableAsync(string? exchangeType);

    Task<Dictionary<string, Exchange>> FindManyAsync(IEnumerable<string> exchangeTypes);

    Task<List<Exchange>> ListConnectableAsync(CancellationToken cancellationToken = default);

    Task<List<AdminExchangeDto>> ListForAdminAsync();
    Task<AdminExchangeDto> CreateAsync(SaveAdminExchangeRequestDto request);
    Task<AdminExchangeDto> UpdateAsync(string exchangeType, SaveAdminExchangeRequestDto request);
}

public sealed class ExchangeCatalog : IExchangeCatalog
{
    private const int MaxNameLength = 100;
    private const int MaxUrlLength = 500;

    private static readonly Regex ExchangeTypeFormat = new("^[A-Z][A-Z0-9_]{1,49}$", RegexOptions.Compiled);
    private static readonly Regex CurrencyFormat = new("^[A-Za-z0-9_.-]{1,50}$", RegexOptions.Compiled);

    private readonly FireblocksDbContext _context;

    public ExchangeCatalog(FireblocksDbContext context)
    {
        _context = context;
    }

    private IQueryable<Exchange> Exchanges => _context.Exchanges.AsNoTracking().Include(exchange => exchange.Currencies);

    public static ExchangeOperationException NotConfigured(Exchange exchange)
    {
        return ExchangeOperationException.BadRequest(
            "EXCHANGE_SIMULATOR_NOT_CONFIGURED",
            $"The {exchange.Name} simulator URL is not configured");
    }

    public Task<Exchange?> FindAsync(string exchangeType, CancellationToken cancellationToken = default)
    {
        return Exchanges.FirstOrDefaultAsync(exchange => exchange.ExchangeType == exchangeType, cancellationToken);
    }

    public async Task<Exchange> RequireAsync(string? exchangeType)
    {
        var exchange = string.IsNullOrWhiteSpace(exchangeType) ? null : await FindAsync(exchangeType);
        return exchange ?? throw ExchangeOperationException.BadRequest(
            "EXCHANGE_TYPE_NOT_SUPPORTED",
            $"Exchange type {exchangeType} is not supported");
    }

    public async Task<Exchange> RequireConnectableAsync(string? exchangeType)
    {
        var exchange = await RequireAsync(exchangeType);
        return exchange.IsConnectable ? exchange : throw NotConfigured(exchange);
    }

    public async Task<Dictionary<string, Exchange>> FindManyAsync(IEnumerable<string> exchangeTypes)
    {
        var types = exchangeTypes.Distinct(StringComparer.Ordinal).ToList();
        return await Exchanges
            .Where(exchange => types.Contains(exchange.ExchangeType))
            .ToDictionaryAsync(exchange => exchange.ExchangeType, StringComparer.Ordinal);
    }

    public Task<List<Exchange>> ListConnectableAsync(CancellationToken cancellationToken = default)
    {
        return Exchanges
            .Where(exchange => exchange.SimulatorBaseUrl != null && exchange.SimulatorBaseUrl != string.Empty)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AdminExchangeDto>> ListForAdminAsync()
    {
        var exchanges = await Exchanges.ToListAsync();
        return exchanges
            .OrderBy(exchange => exchange.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(exchange => exchange.ExchangeType, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList();
    }

    public async Task<AdminExchangeDto> CreateAsync(SaveAdminExchangeRequestDto request)
    {
        var exchangeType = request.ExchangeType?.Trim() ?? string.Empty;
        if (!ExchangeTypeFormat.IsMatch(exchangeType))
        {
            throw ExchangeOperationException.BadRequest(
                "INVALID_EXCHANGE_TYPE",
                "exchangeType must have 2 to 50 characters: capital letters, digits and underscores, starting with a letter");
        }

        if (await _context.Exchanges.AnyAsync(exchange => exchange.ExchangeType == exchangeType))
        {
            throw ExchangeOperationException.Conflict("EXCHANGE_ALREADY_EXISTS", $"Exchange type {exchangeType} already exists");
        }

        var exchange = new Exchange { ExchangeType = exchangeType };
        await ApplyAsync(exchange, request);
        _context.Exchanges.Add(exchange);
        await SaveAsync(exchangeType);
        return ToDto((await FindAsync(exchangeType))!);
    }

    public async Task<AdminExchangeDto> UpdateAsync(string exchangeType, SaveAdminExchangeRequestDto request)
    {
        var exchange = await _context.Exchanges
            .Include(item => item.Currencies)
            .FirstOrDefaultAsync(item => item.ExchangeType == exchangeType)
            ?? throw ExchangeOperationException.NotFound("EXCHANGE_NOT_FOUND", $"Exchange type {exchangeType} not found");

        await ApplyAsync(exchange, request);
        await SaveAsync(exchangeType);
        return ToDto((await FindAsync(exchangeType))!);
    }

    private async Task ApplyAsync(Exchange exchange, SaveAdminExchangeRequestDto request)
    {
        exchange.Name = RequireName(request.Name);
        exchange.SimulatorBaseUrl = ParseSimulatorUrl(request.SimulatorBaseUrl);
        exchange.OneAccountPerWorkspace = request.OneAccountPerWorkspace;
        exchange.UpdatedAt = DateTimeOffset.UtcNow;

        // The rows change in place, so that a kept asset is never removed and added again with the same key.
        var wanted = await ValidateCurrenciesAsync(request.Currencies ?? new List<AdminExchangeCurrencyDto>());
        foreach (var current in exchange.Currencies.ToList())
        {
            var match = wanted.FirstOrDefault(item => item.AssetId == current.AssetId);
            if (match == null)
            {
                exchange.Currencies.Remove(current);
                _context.ExchangeCurrencies.Remove(current);
            }
            else
            {
                current.Currency = match.Currency;
                wanted.Remove(match);
            }
        }

        foreach (var currency in wanted)
        {
            exchange.Currencies.Add(currency);
        }
    }

    private async Task<List<ExchangeCurrency>> ValidateCurrenciesAsync(List<AdminExchangeCurrencyDto> currencies)
    {
        var assetIds = currencies.Select(currency => currency.AssetId?.Trim() ?? string.Empty).ToList();
        var duplicate = assetIds.GroupBy(assetId => assetId, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw ExchangeOperationException.BadRequest("INVALID_CURRENCIES", $"Asset {duplicate.Key} has more than one currency");
        }

        var knownAssets = await _context.Assets
            .Where(asset => assetIds.Contains(asset.AssetId))
            .Select(asset => asset.AssetId)
            .ToListAsync();
        var result = new List<ExchangeCurrency>();
        foreach (var currency in currencies)
        {
            var assetId = currency.AssetId?.Trim() ?? string.Empty;
            var name = currency.Currency?.Trim() ?? string.Empty;
            if (!knownAssets.Contains(assetId, StringComparer.Ordinal))
            {
                throw ExchangeOperationException.BadRequest("INVALID_CURRENCIES", $"Asset {assetId} does not exist");
            }

            if (!CurrencyFormat.IsMatch(name))
            {
                throw ExchangeOperationException.BadRequest(
                    "INVALID_CURRENCIES",
                    $"The currency of {assetId} must have 1 to 50 letters, digits, dots, dashes or underscores");
            }

            result.Add(new ExchangeCurrency { AssetId = assetId, Currency = name });
        }

        return result;
    }

    private async Task SaveAsync(string exchangeType)
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException
        {
            SqlState: Npgsql.PostgresErrorCodes.UniqueViolation,
        })
        {
            throw ExchangeOperationException.Conflict("EXCHANGE_ALREADY_EXISTS", $"Exchange type {exchangeType} already exists");
        }
    }

    private static string RequireName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxNameLength)
        {
            throw ExchangeOperationException.BadRequest("INVALID_EXCHANGE_NAME", $"name must have 1 to {MaxNameLength} characters");
        }

        return trimmed;
    }

    private static string? ParseSimulatorUrl(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > MaxUrlLength ||
            !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw ExchangeOperationException.BadRequest(
                "INVALID_SIMULATOR_URL",
                $"simulatorBaseUrl must be an http or https URL with at most {MaxUrlLength} characters");
        }

        return trimmed.TrimEnd('/');
    }

    private static AdminExchangeDto ToDto(Exchange exchange)
    {
        return new AdminExchangeDto
        {
            ExchangeType = exchange.ExchangeType,
            Name = exchange.Name,
            SimulatorBaseUrl = exchange.SimulatorBaseUrl,
            IsConnectable = exchange.IsConnectable,
            OneAccountPerWorkspace = exchange.OneAccountPerWorkspace,
            Currencies = exchange.Currencies
                .OrderBy(currency => currency.AssetId, StringComparer.Ordinal)
                .Select(currency => new AdminExchangeCurrencyDto { AssetId = currency.AssetId, Currency = currency.Currency })
                .ToList(),
        };
    }
}
