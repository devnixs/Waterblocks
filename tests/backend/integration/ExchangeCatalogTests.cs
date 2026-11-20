using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

/// <summary>
/// Proves that a new exchange needs only catalog data: the tests onboard exchanges through the admin API and use
/// them with no code change.
/// </summary>
public class ExchangeCatalogTests : OffExchangeTestBase
{
    private const string BybitUrl = "http://bybit-simulator.test";
    private const string DeribitUrl = "http://deribit-simulator.test";

    private readonly FakeExchangeSimulator _bybit;
    private readonly FakeExchangeSimulator _deribit;

    public ExchangeCatalogTests()
    {
        _bybit = Network.Add(new FakeExchangeSimulator(BybitUrl));
        _bybit.AddAccount("bybit-main", 20000001, "bybit-key-0123456789abcdef", "bybit-secret-fedcba9876543210");
        _deribit = Network.Add(new FakeExchangeSimulator(DeribitUrl));
        _deribit.AddAccount("deribit-main", 30000001, "deribit-key-0123456789abcdef", "deribit-secret-fedcba9876543210");
    }

    [Fact]
    public async Task Catalog_Starts_With_The_Fireblocks_Exchange_Types_And_The_Gate_Facts()
    {
        var exchanges = (await ReadOkAsync(await Http.GetAsync("/admin/exchanges"))).GetProperty("data").EnumerateArray().ToList();

        exchanges.Select(item => item.GetProperty("exchangeType").GetString()).Should().BeEquivalentTo(
            "INDEPENDENT_RESERVE", "BIT", "BITHUMB", "BITSO", "CRYPTOCOM", "BYBIT_V2", "WHITEBIT", "HITBTC", "GEMINI",
            "HUOBI", "GATEIO", "COINHAKO", "BULLISH", "BITGET", "LUNO");
        var gate = exchanges.Single(item => item.GetProperty("exchangeType").GetString() == GateExchangeType);
        gate.GetProperty("name").GetString().Should().Be("Gate.io");
        gate.GetProperty("simulatorBaseUrl").GetString().Should().Be(GateSimulatorUrl, "the setting fills an empty URL at startup");
        gate.GetProperty("isConnectable").GetBoolean().Should().BeTrue();
        gate.GetProperty("oneAccountPerWorkspace").GetBoolean().Should().BeTrue();
        gate.GetProperty("currencies").EnumerateArray()
            .Select(item => $"{item.GetProperty("assetId").GetString()}={item.GetProperty("currency").GetString()}")
            .Should().Equal("BTC=BTC", "USDT_ERC20=USDT");
        var bybit = exchanges.Single(item => item.GetProperty("exchangeType").GetString() == "BYBIT_V2");
        bybit.GetProperty("isConnectable").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task A_New_Exchange_Works_End_To_End_From_Catalog_Data_Only()
    {
        await SaveExchangeAsync("DERIBIT", "Deribit", DeribitUrl, oneAccountPerWorkspace: true, ("BTC", "BTC"));
        var creds = await EncryptCredsAsync("deribit-key-0123456789abcdef", "deribit-secret-fedcba9876543210");

        var created = await PostAsync(
            "/exchange_accounts",
            new { exchangeType = "DERIBIT", name = "Deribit main", key = "deribit-key-0123456789abcdef", creds });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var accountId = (await ReadJsonAsync(created)).GetProperty("id").GetString()!;
        var account = (await ReadOkAsync(await Http.GetAsync($"/admin/exchange-accounts/{accountId}"))).GetProperty("data");
        account.GetProperty("connection").GetProperty("simulatorAccountName").GetString().Should().Be("deribit-main");

        var vaultId = await CreateFundedVaultAsync();
        var addId = await AddAndCompleteAsync(vaultId, accountId, "1");
        await WaitForMirrorStateAsync(accountId, addId, "APPLIED");
        _deribit.FindEvent(addId)!.AssetId.Should().Be("BTC");
        _deribit.GetSpotAvailable("deribit-main", "BTC").Should().Be(0.9999m);
        Simulator.EventRequests.Should().Be(0, "the Gate simulator must not get the events of another exchange");

        var second = await PostAsync("/exchange_accounts", new { exchangeType = "DERIBIT", name = "Deribit two" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(second)).GetProperty("message").GetString()
            .Should().Be("This workspace already has a Deribit account. A workspace can have only one Deribit account");
    }

    [Fact]
    public async Task A_Seeded_Exchange_Connects_After_An_Admin_Sets_Its_Simulator_Url()
    {
        await SaveExchangeAsync("BYBIT_V2", "Bybit", BybitUrl, oneAccountPerWorkspace: false, ("BTC", "BTC"), ("USDC", "USDC"));

        var accountId = await CreateConnectedAccountAsync("Bybit main", "BYBIT_V2", "bybit-key-0123456789abcdef", "bybit-secret-fedcba9876543210");
        var vaultId = await CreateFundedVaultAsync();
        var addId = await AddAndCompleteAsync(vaultId, accountId, "1");
        await WaitForMirrorStateAsync(accountId, addId, "APPLIED");

        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");
        var removalId = removal.GetProperty("id").GetString()!;
        await WaitForMirrorStateAsync(accountId, removalId, "APPLIED");
        await CompleteAsync(removalId);

        _bybit.GetSpotAvailable("bybit-main", "BTC").Should().Be(0.5999m);
        Amount(await GetExchangeAssetAsync(accountId), "available").Should().Be(0.5999m);
        Simulator.EventRequests.Should().Be(0);
        var secondBybit = await CreateExchangeAccountAsync("Bybit two", "BYBIT_V2");
        secondBybit.Should().NotBe(accountId, "Bybit has no one-account rule");
    }

    [Fact]
    public async Task A_Connected_Account_Accepts_Only_The_Currencies_Of_Its_Exchange()
    {
        await SaveExchangeAsync("BYBIT_V2", "Bybit", BybitUrl, oneAccountPerWorkspace: false, ("BTC", "BTC"));
        var accountId = await CreateConnectedAccountAsync("Bybit main", "BYBIT_V2", "bybit-key-0123456789abcdef", "bybit-secret-fedcba9876543210");
        var vaultId = await CreateFundedVaultAsync();

        var response = await PostAsync("/off_exchange/add", AddBody(vaultId, accountId, "1", assetId: "ETH"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString().Should().Be("A connected Bybit account does not accept ETH");
    }

    [Fact]
    public async Task Two_Exchanges_Can_Connect_Simulator_Accounts_With_The_Same_Id()
    {
        _bybit.AddAccount("bybit-same-id", SimulatorUserId, "bybit-same-id-key-0001", "bybit-same-id-secret-0001");
        await SaveExchangeAsync("BYBIT_V2", "Bybit", BybitUrl, oneAccountPerWorkspace: false, ("BTC", "BTC"));

        await CreateConnectedAccountAsync();
        var bybitId = await CreateConnectedAccountAsync("Bybit same id", "BYBIT_V2", "bybit-same-id-key-0001", "bybit-same-id-secret-0001");

        var bybit = (await ReadOkAsync(await Http.GetAsync($"/admin/exchange-accounts/{bybitId}"))).GetProperty("data");
        bybit.GetProperty("connection").GetProperty("simulatorAccountId").GetString().Should().Be(SimulatorUserId.ToString());
    }

    [Fact]
    public async Task An_Exchange_Without_A_Simulator_Url_Cannot_Connect()
    {
        var response = await PostAdminCreateAsync(Http, "BITGET", "Bitget", "bybit-key-0123456789abcdef", "bybit-secret-fedcba9876543210");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = (await ReadJsonAsync(response)).GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("EXCHANGE_SIMULATOR_NOT_CONFIGURED");
        error.GetProperty("message").GetString().Should().Be("The Bitget simulator URL is not configured");
    }

    [Fact]
    public async Task An_Exchange_Type_Outside_The_Catalog_Is_Refused()
    {
        var response = await PostAsync("/exchange_accounts", new { exchangeType = "DERIBIT", name = "Deribit" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString().Should().Be("Exchange type DERIBIT is not supported");
    }

    [Theory]
    [InlineData("deribit", "Deribit", null, "BTC", "INVALID_EXCHANGE_TYPE")]
    [InlineData("DERIBIT", "", null, "BTC", "INVALID_EXCHANGE_NAME")]
    [InlineData("DERIBIT", "Deribit", "ftp://deribit", "BTC", "INVALID_SIMULATOR_URL")]
    [InlineData("DERIBIT", "Deribit", null, "NOT_AN_ASSET", "INVALID_CURRENCIES")]
    public async Task Catalog_Refuses_Invalid_Exchanges(
        string exchangeType,
        string name,
        string? simulatorBaseUrl,
        string assetId,
        string code)
    {
        var response = await Http.PostAsJsonAsync("/admin/exchanges", new
        {
            exchangeType,
            name,
            simulatorBaseUrl,
            currencies = new[] { new { assetId, currency = "BTC" } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("error").GetProperty("code").GetString().Should().Be(code);
    }

    [Fact]
    public async Task Catalog_Refuses_A_Duplicate_And_An_Unknown_Update()
    {
        var duplicate = await Http.PostAsJsonAsync("/admin/exchanges", new { exchangeType = GateExchangeType, name = "Gate again" });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var unknown = await Http.PutAsJsonAsync("/admin/exchanges/NOPE", new { name = "Nope" });
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Catalog_Update_Replaces_The_Currencies()
    {
        await SaveExchangeAsync(GateExchangeType, "Gate.io", GateSimulatorUrl, oneAccountPerWorkspace: true, ("BTC", "BTC"), ("USDC", "USDC"));

        var gate = (await ReadOkAsync(await Http.GetAsync("/admin/exchanges"))).GetProperty("data").EnumerateArray()
            .Single(item => item.GetProperty("exchangeType").GetString() == GateExchangeType);
        gate.GetProperty("currencies").EnumerateArray().Select(item => item.GetProperty("assetId").GetString())
            .Should().Equal("BTC", "USDC");
    }
}
