using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Waterblocks.Api.Services.ExchangeSimulators;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

/// <summary>
/// Runs a second exchange next to Gate.io, through a test client only, to prove that the exchange code is generic.
/// </summary>
public class OffExchangeProviderTests : OffExchangeTestBase
{
    private const string BybitExchangeType = "BYBIT_V2";
    private const string BybitAccount = "bybit-main";
    private const string BybitAccountId = "20000001";
    private const string BybitApiKey = "bybit-key-0123456789abcdef";
    private const string BybitApiSecret = "bybit-secret-fedcba9876543210";

    private const string DeribitExchangeType = "DERIBIT";

    private readonly FakeExchangeSimulatorClient _bybit = new(
        BybitExchangeType,
        "Bybit",
        new[] { "BTC", "USDC" },
        oneAccountPerWorkspace: false);

    private readonly FakeExchangeSimulatorClient _deribit = new(
        DeribitExchangeType,
        "Deribit",
        new[] { "BTC" },
        oneAccountPerWorkspace: true);

    public OffExchangeProviderTests()
    {
        _bybit.AddAccount(BybitAccountId, BybitAccount, BybitApiKey, BybitApiSecret);
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddSingleton<IExchangeSimulatorClient>(_bybit);
        services.AddSingleton<IExchangeSimulatorClient>(_deribit);
    }

    [Fact]
    public async Task Providers_List_Each_Exchange_With_Its_Rules()
    {
        var providers = (await ReadOkAsync(await Http.GetAsync("/admin/exchange-providers"))).GetProperty("data");

        providers.EnumerateArray().Select(item => item.GetProperty("exchangeType").GetString())
            .Should().Equal(BybitExchangeType, DeribitExchangeType, GateExchangeType);

        var gate = providers.EnumerateArray().Single(item => item.GetProperty("exchangeType").GetString() == GateExchangeType);
        gate.GetProperty("name").GetString().Should().Be("Gate.io");
        gate.GetProperty("assetIds").EnumerateArray().Select(item => item.GetString()).Should().Equal("BTC", "USDT");
        gate.GetProperty("oneAccountPerWorkspace").GetBoolean().Should().BeTrue();
        gate.GetProperty("isConfigured").GetBoolean().Should().BeTrue();

        var bybit = providers.EnumerateArray().Single(item => item.GetProperty("exchangeType").GetString() == BybitExchangeType);
        bybit.GetProperty("name").GetString().Should().Be("Bybit");
        bybit.GetProperty("assetIds").EnumerateArray().Select(item => item.GetString()).Should().Equal("BTC", "USDC");
        bybit.GetProperty("oneAccountPerWorkspace").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Providers_Show_A_Simulator_With_No_Url_As_Not_Configured()
    {
        await using var factory = Fixture.CreateFactory(SimulatorSettings(baseUrl: string.Empty), ConfigureServices);
        var client = factory.CreateClient();

        var providers = (await ReadOkAsync(await client.GetAsync("/admin/exchange-providers"))).GetProperty("data");

        var gate = providers.EnumerateArray().Single(item => item.GetProperty("exchangeType").GetString() == GateExchangeType);
        gate.GetProperty("isConfigured").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task A_Second_Exchange_Connects_And_Gets_Its_Own_Mirror_Events()
    {
        var accountId = await CreateConnectedAccountAsync("Bybit main", BybitExchangeType, BybitApiKey, BybitApiSecret);
        var account = (await ReadOkAsync(await Http.GetAsync($"/admin/exchange-accounts/{accountId}"))).GetProperty("data");
        account.GetProperty("exchangeType").GetString().Should().Be(BybitExchangeType);
        account.GetProperty("connection").GetProperty("simulatorAccountName").GetString().Should().Be(BybitAccount);
        account.GetProperty("connection").GetProperty("simulatorAccountId").GetString().Should().Be(BybitAccountId);
        account.GetProperty("assets").EnumerateArray().Select(item => item.GetProperty("assetId").GetString())
            .Should().Equal("BTC", "USDC");

        var vaultId = await CreateFundedVaultAsync();
        var addId = await AddAndCompleteAsync(vaultId, accountId, "1");
        await WaitForMirrorStateAsync(accountId, addId, "APPLIED");
        _bybit.GetAvailable(BybitAccount, "BTC").Should().Be(0.9999m);

        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");
        var removalId = removal.GetProperty("id").GetString()!;
        await WaitForMirrorStateAsync(accountId, removalId, "APPLIED");
        await CompleteAsync(removalId);

        _bybit.GetAvailable(BybitAccount, "BTC").Should().Be(0.5999m);
        _bybit.GetEvents().Select(item => item.ExchangeType).Should().OnlyContain(type => type == BybitExchangeType);
        Simulator.EventRequests.Should().Be(0, "the Gate simulator must not receive the events of another exchange");
        Amount(await GetExchangeAssetAsync(accountId), "available").Should().Be(0.5999m);
    }

    [Fact]
    public async Task A_Connected_Account_Accepts_Only_The_Assets_Of_Its_Exchange()
    {
        var accountId = await CreateConnectedAccountAsync("Bybit main", BybitExchangeType, BybitApiKey, BybitApiSecret);
        var vaultId = await CreateFundedVaultAsync();

        var response = await PostAsync("/off_exchange/add", AddBody(vaultId, accountId, "1", assetId: "ETH"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString()
            .Should().Be("A connected Bybit account does not accept ETH");
    }

    [Fact]
    public async Task An_Exchange_Without_The_One_Account_Rule_Allows_More_Accounts_In_A_Workspace()
    {
        var first = await CreateExchangeAccountAsync("Bybit one", BybitExchangeType);
        var second = await CreateExchangeAccountAsync("Bybit two", BybitExchangeType);

        first.Should().NotBe(second);
    }

    [Fact]
    public async Task A_Simulator_Client_Makes_Its_Exchange_Type_Valid_And_Applies_Its_One_Account_Rule()
    {
        await CreateExchangeAccountAsync("Deribit one", DeribitExchangeType);

        var second = await PostAsync("/exchange_accounts", new { exchangeType = DeribitExchangeType, name = "Deribit two" });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(second)).GetProperty("message").GetString()
            .Should().Be("This workspace already has a Deribit account. A workspace can have only one Deribit account");
        await CreateExchangeAccountAsync("Gate next to Deribit");
    }

    [Fact]
    public async Task Two_Exchanges_Can_Connect_Simulator_Accounts_With_The_Same_Id()
    {
        _bybit.AddAccount(SimulatorUserId.ToString(), "bybit-same-id", "bybit-same-id-key", "bybit-same-id-secret");

        await CreateConnectedAccountAsync();
        var bybitId = await CreateConnectedAccountAsync(
            "Bybit same id",
            BybitExchangeType,
            "bybit-same-id-key",
            "bybit-same-id-secret");

        var bybit = (await ReadOkAsync(await Http.GetAsync($"/admin/exchange-accounts/{bybitId}"))).GetProperty("data");
        bybit.GetProperty("connection").GetProperty("simulatorAccountId").GetString().Should().Be(SimulatorUserId.ToString());
    }

    [Fact]
    public async Task An_Exchange_Type_Without_A_Simulator_Cannot_Connect()
    {
        var response = await PostAsync(
            "/admin/exchange-accounts",
            new { exchangeType = "BITGET", name = "Bitget", apiKey = BybitApiKey, apiSecret = BybitApiSecret });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = (await ReadJsonAsync(response)).GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("EXCHANGE_TYPE_NOT_SUPPORTED");
        error.GetProperty("message").GetString().Should().Be("Exchange type BITGET has no exchange simulator");
    }
}
