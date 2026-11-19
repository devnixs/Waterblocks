using System.Net;
using System.Text.Json;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services.ExchangeSimulators;

/// <summary>
/// The settings, the HTTP calls and the error replies that all exchange simulator clients share.
/// </summary>
public abstract class ExchangeSimulatorClientBase : IExchangeSimulatorClient
{
    private readonly IConfiguration _configuration;

    protected ExchangeSimulatorClientBase(HttpClient httpClient, IConfiguration configuration)
    {
        HttpClient = httpClient;
        _configuration = configuration;
    }

    public abstract string ExchangeType { get; }
    public abstract string DisplayName { get; }
    public abstract IReadOnlyList<string> AssetIds { get; }
    public abstract bool OneAccountPerWorkspace { get; }

    public bool IsConfigured =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri) &&
        (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps);

    protected HttpClient HttpClient { get; }

    protected string BaseUrl =>
        _configuration[ExchangeSimulatorSettings.BaseUrlKey(ExchangeType)]?.Trim().TrimEnd('/') ?? string.Empty;

    protected string? AdminToken => _configuration[ExchangeSimulatorSettings.AdminTokenKey(ExchangeType)]?.Trim();

    public abstract Task<SimulatorAccountIdentity> IdentifyAccountAsync(
        string apiKey,
        string apiSecret,
        CancellationToken cancellationToken = default);

    public abstract Task<SimulatorAccountSnapshot?> TryReadAccountAsync(
        string accountName,
        CancellationToken cancellationToken = default);

    public abstract Task<MirrorEventOutcome> SendEventAsync(
        ExchangeMirrorEvent mirrorEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request. Returns null when the simulator cannot be reached or does not answer in time.
    /// </summary>
    protected async Task<HttpResponseMessage?> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await HttpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a JSON reply. Returns null when the body is not JSON or cannot be read.
    /// </summary>
    protected static async Task<JsonDocument?> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    protected static string? ReadString(JsonDocument? document, string propertyName)
    {
        if (document?.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    protected ExchangeOperationException NotConfigured()
    {
        return ExchangeOperationException.BadRequest(
            "EXCHANGE_SIMULATOR_NOT_CONFIGURED",
            $"The {DisplayName} simulator URL is not configured");
    }

    protected ExchangeOperationException InvalidCredentials()
    {
        return ExchangeOperationException.BadRequest(
            "INVALID_EXCHANGE_CREDENTIALS",
            $"The {DisplayName} simulator refused this API key or secret");
    }

    protected ExchangeOperationException AccountNotFound()
    {
        return ExchangeOperationException.BadRequest(
            "SIMULATOR_ACCOUNT_NOT_FOUND",
            $"The {DisplayName} simulator has no account for this API key");
    }

    protected ExchangeOperationException Unreachable()
    {
        return new ExchangeOperationException(
            HttpStatusCode.BadGateway,
            "EXCHANGE_SIMULATOR_UNREACHABLE",
            $"The {DisplayName} simulator cannot be reached");
    }

    protected MirrorEventOutcome NotConfiguredOutcome()
    {
        return new MirrorEventOutcome(MirrorEventOutcomeKind.Retry, $"The {DisplayName} simulator URL is not configured");
    }

    protected MirrorEventOutcome UnreachableOutcome()
    {
        return new MirrorEventOutcome(MirrorEventOutcomeKind.Retry, $"The {DisplayName} simulator cannot be reached");
    }
}
