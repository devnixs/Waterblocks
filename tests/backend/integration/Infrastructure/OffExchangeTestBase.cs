using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Waterblocks.Api.Services;
using Xunit;

namespace Waterblocks.IntegrationTests.Infrastructure;

/// <summary>
/// Shared setup and calls for the Off Exchange integration tests.
/// </summary>
public abstract class OffExchangeTestBase : IAsyncLifetime
{
    protected const string SimulatorAccount = "lombard";
    protected const string SimulatorApiKey = "gate-key-0123456789abcdef";
    protected const string SimulatorApiSecret = "gate-secret-fedcba9876543210";
    protected const long SimulatorUserId = 10000001;
    protected const decimal BtcFee = 0.0001m;

    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(20);

    protected OffExchangeTestBase()
    {
        Simulator.AddAccount(SimulatorAccount, SimulatorUserId, SimulatorApiKey, SimulatorApiSecret);
        Fixture = IntegrationTestFixture.Create(SimulatorSettings(), ConfigureServices);
    }

    protected FakeGateSimulator Simulator { get; } = new();
    protected CapturingLogSink Logs { get; } = new();
    protected IntegrationTestFixture Fixture { get; }
    protected HttpClient Http => Fixture.HttpClient;

    public Task InitializeAsync() => Fixture.InitializeAsync();
    public Task DisposeAsync() => Fixture.DisposeAsync();

    protected static IReadOnlyDictionary<string, string?> SimulatorSettings(string? baseUrl = FakeGateSimulator.BaseUrl)
    {
        return new Dictionary<string, string?> { [GateSimulatorClient.BaseUrlSetting] = baseUrl };
    }

    protected void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient<IGateSimulatorClient, GateSimulatorClient>()
            .ConfigurePrimaryHttpMessageHandler(() => Simulator);
        services.AddSingleton<ILogEventSink>(Logs);
    }

    protected Task<HttpResponseMessage> PostAsync(string path, object body, string? idempotencyKey = null)
    {
        return PostAsync(Http, path, body, idempotencyKey);
    }

    protected static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        object body,
        string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (idempotencyKey != null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return client.SendAsync(request);
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    protected static async Task<JsonElement> ReadOkAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(text);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    protected static decimal Amount(JsonElement element, string propertyName)
    {
        return decimal.Parse(element.GetProperty(propertyName).GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    protected async Task<string> CreateExchangeAccountAsync(string name = "Gate main", string exchangeType = "GATEIO")
    {
        var response = await PostAsync("/exchange_accounts", new { exchangeType, name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadJsonAsync(response)).GetProperty("id").GetString()!;
    }

    protected async Task<string> CreateConnectedAccountAsync(string name = "Gate connected")
    {
        var response = await PostAsync(
            "/admin/exchange-accounts",
            new { exchangeType = "GATEIO", name, apiKey = SimulatorApiKey, apiSecret = SimulatorApiSecret });
        var body = await ReadOkAsync(response);
        return body.GetProperty("data").GetProperty("id").GetString()!;
    }

    protected async Task<string> CreateFundedVaultAsync(string assetId = "BTC", decimal amount = 2m, string name = "Loan vault")
    {
        var vault = await Fixture.AdminClient.CreateVaultAsync(name);
        vault.IsSuccess.Should().BeTrue(vault.Error?.Message);
        var wallet = await Fixture.AdminClient.CreateWalletAsync(vault.Data!.Id, assetId);
        wallet.IsSuccess.Should().BeTrue(wallet.Error?.Message);
        var deposit = await Fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = assetId,
            DestinationAddress = wallet.Data!.DepositAddress,
            Amount = amount.ToString(CultureInfo.InvariantCulture),
        });
        deposit.IsSuccess.Should().BeTrue(deposit.Error?.Message);
        return vault.Data.Id;
    }

    protected static object AddBody(
        string vaultId,
        string exchangeAccountId,
        string amount,
        bool gross = true,
        string assetId = "BTC",
        string? externalTxId = null,
        string? note = null)
    {
        return new
        {
            transactionRequest = new
            {
                assetId,
                source = new { type = "VAULT_ACCOUNT", id = vaultId },
                destination = new { type = "EXCHANGE_ACCOUNT", id = exchangeAccountId, isCollateral = true },
                amount,
                treatAsGrossAmount = gross,
                externalTxId,
                note,
            },
            isSrcCollateral = false,
        };
    }

    protected static object RemoveBody(
        string vaultId,
        string exchangeAccountId,
        string amount,
        bool gross = true,
        string assetId = "BTC",
        string? externalTxId = null)
    {
        return new
        {
            transactionRequest = new
            {
                assetId,
                source = new { type = "EXCHANGE_ACCOUNT", id = exchangeAccountId, isCollateral = true },
                destination = new { type = "VAULT_ACCOUNT", id = vaultId },
                amount,
                treatAsGrossAmount = gross,
                externalTxId,
            },
            isDstCollateral = true,
        };
    }

    protected async Task<string> SubmitAddAsync(string vaultId, string accountId, string amount, bool gross = true)
    {
        var response = await PostAsync("/off_exchange/add", AddBody(vaultId, accountId, amount, gross));
        var body = await ReadOkAsync(response);
        body.GetProperty("status").GetString().Should().Be("SUBMITTED");
        return body.GetProperty("id").GetString()!;
    }

    protected async Task<JsonElement> SubmitRemoveAsync(string vaultId, string accountId, string amount, bool gross = true)
    {
        var response = await PostAsync("/off_exchange/remove", RemoveBody(vaultId, accountId, amount, gross));
        return await ReadOkAsync(response);
    }

    protected async Task CompleteAsync(string transactionId)
    {
        var result = await Fixture.AdminClient.CompleteTransactionFullCycleAsync(transactionId);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Data!.State.Should().Be("COMPLETED");
    }

    protected async Task<string> AddAndCompleteAsync(string vaultId, string accountId, string amount, bool gross = true)
    {
        var transactionId = await SubmitAddAsync(vaultId, accountId, amount, gross);
        await CompleteAsync(transactionId);
        return transactionId;
    }

    protected async Task<JsonElement> GetExchangeAssetAsync(string accountId, string assetId = "BTC")
    {
        return await ReadOkAsync(await Http.GetAsync($"/exchange_accounts/{accountId}/{assetId}"));
    }

    protected async Task<JsonElement> GetTransactionAsync(string transactionId)
    {
        return await ReadOkAsync(await Http.GetAsync($"/transactions/{transactionId}"));
    }

    protected async Task<JsonElement> GetTransferAsync(string accountId, string transactionId)
    {
        var body = await ReadOkAsync(await Http.GetAsync($"/admin/exchange-accounts/{accountId}/transfers"));
        return body.GetProperty("data")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == transactionId);
    }

    protected async Task<(decimal Balance, decimal Pending)> GetVaultBalanceAsync(string vaultId, string assetId = "BTC")
    {
        var vault = await Fixture.AdminClient.GetVaultAsync(vaultId);
        vault.IsSuccess.Should().BeTrue(vault.Error?.Message);
        var wallet = vault.Data!.Wallets.Single(item => item.AssetId == assetId);
        return (decimal.Parse(wallet.Balance, CultureInfo.InvariantCulture), decimal.Parse(wallet.Pending, CultureInfo.InvariantCulture));
    }

    protected static async Task WaitUntilAsync(Func<Task<bool>> condition, string description, TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? DefaultWait);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Timed out while waiting: {description}");
    }

    protected Task WaitForStateAsync(string transactionId, string state, TimeSpan? timeout = null)
    {
        return WaitUntilAsync(
            async () => (await GetTransactionAsync(transactionId)).GetProperty("status").GetString() == state,
            $"transaction {transactionId} reaches {state}",
            timeout);
    }

    protected Task WaitForMirrorStateAsync(string accountId, string transactionId, string mirrorState)
    {
        return WaitUntilAsync(
            async () =>
            {
                var transfer = await GetTransferAsync(accountId, transactionId);
                return transfer.TryGetProperty("mirrorState", out var value) && value.GetString() == mirrorState;
            },
            $"transaction {transactionId} reaches mirror state {mirrorState}");
    }
}
