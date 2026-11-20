using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class OffExchangeAccountTests : OffExchangeTestBase
{
    private static readonly string[] OtherExchangeTypes = { "BITGET", "BYBIT_V2", "GEMINI" };

    [Fact]
    public async Task Exchange_Accounts_Can_Be_Created_Listed_Paged_And_Read()
    {
        var created = await PostAsync("/exchange_accounts", new { exchangeType = "GATEIO", name = "Gate A" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(created);
        var firstId = body.GetProperty("id").GetString()!;
        Guid.TryParse(firstId, out _).Should().BeTrue();
        body.GetProperty("name").GetString().Should().Be("Gate A");
        body.GetProperty("exchangeType").GetString().Should().Be("GATEIO");

        var ids = new List<string> { firstId };
        foreach (var exchangeType in OtherExchangeTypes)
        {
            ids.Add(await CreateExchangeAccountAsync($"Account {exchangeType}", exchangeType));
        }

        var list = await ReadOkAsync(await Http.GetAsync("/exchange_accounts"));
        list.EnumerateArray().Select(item => item.GetProperty("id").GetString()).Should().BeEquivalentTo(ids);

        var firstPage = await ReadOkAsync(await Http.GetAsync("/exchange_accounts/paged"));
        firstPage.GetProperty("exchanges").GetArrayLength().Should().Be(3);
        var after = firstPage.GetProperty("paging").GetProperty("after").GetString();
        firstPage.GetProperty("paging").TryGetProperty("before", out _).Should().BeFalse();

        var secondPage = await ReadOkAsync(await Http.GetAsync($"/exchange_accounts/paged?limit=3&after={after}"));
        secondPage.GetProperty("exchanges").GetArrayLength().Should().Be(1);
        secondPage.GetProperty("paging").TryGetProperty("after", out _).Should().BeFalse();
        var before = secondPage.GetProperty("paging").GetProperty("before").GetString();

        var previousPage = await ReadOkAsync(await Http.GetAsync($"/exchange_accounts/paged?limit=3&before={before}"));
        previousPage.GetProperty("exchanges").EnumerateArray().Select(item => item.GetProperty("id").GetString())
            .Should().Equal(firstPage.GetProperty("exchanges").EnumerateArray().Select(item => item.GetProperty("id").GetString()));

        var account = await ReadOkAsync(await Http.GetAsync($"/exchange_accounts/{firstId}"));
        account.GetProperty("type").GetString().Should().Be("GATEIO");
        account.GetProperty("status").GetString().Should().Be("APPROVED");
        account.GetProperty("name").GetString().Should().Be("Gate A");
        account.GetProperty("isSubaccount").GetBoolean().Should().BeFalse();
        account.GetProperty("success").GetBoolean().Should().BeTrue();
        account.GetProperty("assets").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Main_Account_Id_Marks_A_Sub_Account()
    {
        var created = await PostAsync(
            "/exchange_accounts",
            new { exchangeType = "BITGET", name = "Sub", mainAccountId = "main-1" });
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetString();

        var account = await ReadOkAsync(await Http.GetAsync($"/exchange_accounts/{id}"));
        account.GetProperty("isSubaccount").GetBoolean().Should().BeTrue();
        account.GetProperty("mainAccountId").GetString().Should().Be("main-1");
    }

    [Theory]
    [InlineData("NOT_AN_EXCHANGE")]
    [InlineData("KRAKEN")]
    public async Task Unknown_Exchange_Type_Gets_400(string exchangeType)
    {
        var response = await PostAsync("/exchange_accounts", new { exchangeType, name = "Gate" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Page_Limit_Outside_The_Range_Gets_400(int limit)
    {
        var response = await Http.GetAsync($"/exchange_accounts/paged?limit={limit}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Another_Workspace_Gets_404()
    {
        var accountId = await CreateExchangeAccountAsync();
        var (otherWorkspaceId, otherApiKey) = await Fixture.CreateWorkspaceAsync("Other workspace");
        var other = Fixture.CreateAnonymousHttpClient();
        other.DefaultRequestHeaders.Add("X-API-Key", otherApiKey);
        other.DefaultRequestHeaders.Add("X-Workspace-Id", otherWorkspaceId);

        (await other.GetAsync($"/exchange_accounts/{accountId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.GetAsync($"/exchange_accounts/{accountId}/BTC")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.GetAsync($"/off_exchange/collateral_accounts/{accountId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.GetAsync($"/admin/exchange-accounts/{accountId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await ReadOkAsync(await other.GetAsync("/exchange_accounts"));
        list.GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("GET", "/exchange_accounts")]
    [InlineData("GET", "/exchange_accounts/paged")]
    [InlineData("GET", "/exchange_accounts/credentials_public_key")]
    [InlineData("POST", "/exchange_accounts")]
    [InlineData("POST", "/off_exchange/add")]
    [InlineData("POST", "/off_exchange/remove")]
    [InlineData("GET", "/off_exchange/collateral_accounts/any")]
    public async Task Missing_Or_Unknown_Api_Key_Gets_401(string method, string path)
    {
        var anonymous = Fixture.CreateAnonymousHttpClient();
        var missingKey = await anonymous.SendAsync(CreateRequest(method, path));
        missingKey.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        anonymous.DefaultRequestHeaders.Add("X-API-Key", "not-a-real-key");
        var unknownKey = await anonymous.SendAsync(CreateRequest(method, path));
        unknownKey.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Credentials_Public_Key_Is_A_Real_Rsa_Key_Of_The_Workspace()
    {
        var body = await ReadOkAsync(await Http.GetAsync("/exchange_accounts/credentials_public_key"));
        var publicKey = body.GetProperty("publicKey").GetString()!;
        publicKey.Should().StartWith("-----BEGIN PUBLIC KEY-----").And.EndWith("-----END PUBLIC KEY-----");
        using var rsa = System.Security.Cryptography.RSA.Create();
        rsa.ImportFromPem(publicKey);
        rsa.KeySize.Should().BeGreaterThanOrEqualTo(2048);
        body.GetProperty("tenantId").GetString().Should().Be(Fixture.WorkspaceId);

        var admin = (await ReadOkAsync(await Http.GetAsync("/admin/exchange-accounts/credentials-public-key"))).GetProperty("data");
        admin.GetProperty("publicKey").GetString().Should().Be(publicKey);
        admin.GetProperty("tenantId").GetString().Should().Be(Fixture.WorkspaceId);
    }

    [Fact]
    public async Task Admin_Create_Connects_A_Valid_Key_And_Masks_It()
    {
        var response = await PostAdminCreateAsync(Http, GateExchangeType, "Gate main", SimulatorApiKey, SimulatorApiSecret);

        var data = (await ReadOkAsync(response)).GetProperty("data");
        data.GetProperty("name").GetString().Should().Be("Gate main");
        var connection = data.GetProperty("connection");
        connection.GetProperty("simulatorAccountName").GetString().Should().Be(SimulatorAccount);
        connection.GetProperty("simulatorAccountId").GetString().Should().Be(SimulatorUserId.ToString());
        connection.GetProperty("maskedApiKey").GetString().Should().Be("gate****cdef");
        data.GetProperty("assets").EnumerateArray().Select(asset => asset.GetProperty("assetId").GetString())
            .Should().Equal("BTC", "USDT_ERC20");
        Simulator.VerifyRequests.Should().Be(1);
    }

    [Fact]
    public async Task Connect_Links_An_Account_That_Has_No_Connection()
    {
        var accountId = await CreateExchangeAccountAsync();
        var before = await ReadOkAsync(await Http.GetAsync($"/admin/exchange-accounts/{accountId}"));
        before.GetProperty("data").TryGetProperty("connection", out _).Should().BeFalse();

        var response = await PostAdminConnectAsync(Http, accountId, SimulatorApiKey, SimulatorApiSecret);

        var data = (await ReadOkAsync(response)).GetProperty("data");
        data.GetProperty("connection").GetProperty("simulatorAccountName").GetString().Should().Be(SimulatorAccount);
    }

    [Fact]
    public async Task Admin_Connect_Takes_Only_Encrypted_Creds()
    {
        var plain = await PostAsync(
            "/admin/exchange-accounts",
            new { exchangeType = GateExchangeType, name = "Plain", apiKey = SimulatorApiKey, apiSecret = SimulatorApiSecret });

        plain.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(plain)).GetProperty("error").GetProperty("code").GetString().Should().Be("API_CREDENTIALS_REQUIRED");
        Simulator.VerifyRequests.Should().Be(0);
    }

    [Fact]
    public async Task Fireblocks_Add_With_Key_And_Creds_Connects_The_Account()
    {
        var creds = await EncryptCredsAsync(SimulatorApiKey, SimulatorApiSecret);

        var created = await PostAsync(
            "/exchange_accounts",
            new { exchangeType = GateExchangeType, name = "Gate by API", key = SimulatorApiKey, creds });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var accountId = (await ReadJsonAsync(created)).GetProperty("id").GetString();
        var account = (await ReadOkAsync(await Http.GetAsync($"/admin/exchange-accounts/{accountId}"))).GetProperty("data");
        account.GetProperty("connection").GetProperty("simulatorAccountName").GetString().Should().Be(SimulatorAccount);
    }

    [Fact]
    public async Task Fireblocks_Add_Refuses_Creds_That_Are_Not_Valid_For_The_Call()
    {
        await AssertFireblocksAddFailsAsync(
            "not-base64-creds",
            SimulatorApiKey,
            "creds must be the Base64 RSA-OAEP SHA-256 encryption of {\"apiKey\",\"secret\",\"tenantId\"} with the credentials public key");
        await AssertFireblocksAddFailsAsync(
            await EncryptCredsAsync(SimulatorApiKey, SimulatorApiSecret, tenantId: Guid.NewGuid().ToString()),
            SimulatorApiKey,
            "The tenantId in creds is not the tenant id of this workspace");
        await AssertFireblocksAddFailsAsync(
            await EncryptCredsAsync(SimulatorApiKey, SimulatorApiSecret),
            "another-api-key",
            "The key does not match the apiKey in creds");
        await AssertFireblocksAddFailsAsync(
            await EncryptCredsAsync(SimulatorApiKey, "wrong-secret"),
            SimulatorApiKey,
            "The Gate.io simulator refused this API key or secret");

        (await ReadOkAsync(await Http.GetAsync("/exchange_accounts"))).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Wrong_Key_Or_Secret_Gives_Invalid_Credentials()
    {
        await AssertConnectFailsAsync(HttpStatusCode.BadRequest, "INVALID_EXCHANGE_CREDENTIALS", "wrong-key", SimulatorApiSecret);
        await AssertConnectFailsAsync(HttpStatusCode.BadRequest, "INVALID_EXCHANGE_CREDENTIALS", SimulatorApiKey, "wrong-secret");
    }

    [Fact]
    public async Task Unreachable_Simulator_Gives_Unreachable_Code()
    {
        Simulator.IsOffline = true;
        await AssertConnectFailsAsync(HttpStatusCode.BadGateway, "EXCHANGE_SIMULATOR_UNREACHABLE", SimulatorApiKey, SimulatorApiSecret);

        Simulator.IsOffline = false;
        Simulator.ForcedStatus = HttpStatusCode.ServiceUnavailable;
        await AssertConnectFailsAsync(HttpStatusCode.BadGateway, "EXCHANGE_SIMULATOR_UNREACHABLE", SimulatorApiKey, SimulatorApiSecret);
    }

    [Fact]
    public async Task An_Exchange_With_No_Simulator_Url_Gives_Not_Configured_Code()
    {
        await SaveExchangeAsync(GateExchangeType, "Gate.io", simulatorBaseUrl: null, oneAccountPerWorkspace: true, ("BTC", "BTC"));

        var response = await PostAdminCreateAsync(Http, GateExchangeType, "Gate", SimulatorApiKey, SimulatorApiSecret);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = (await ReadJsonAsync(response)).GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("EXCHANGE_SIMULATOR_NOT_CONFIGURED");
        error.GetProperty("message").GetString().Should().Be("The Gate.io simulator URL is not configured");
        Simulator.VerifyRequests.Should().Be(0);
    }

    [Fact]
    public async Task Admin_Token_Is_Sent_On_Simulator_Admin_Calls()
    {
        Simulator.RequiredAdminToken = "simulator-admin-token";
        await AssertConnectFailsAsync(HttpStatusCode.BadGateway, "EXCHANGE_SIMULATOR_UNAUTHORIZED", SimulatorApiKey, SimulatorApiSecret);

        var settings = new Dictionary<string, string?>(SimulatorSettings())
        {
            [Waterblocks.Api.Services.ExchangeSimulators.ExchangeSimulatorSettings.AdminTokenKey(GateExchangeType)] =
                "simulator-admin-token",
        };
        await using var factory = Fixture.CreateFactory(settings, ConfigureServices);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Workspace-Id", Fixture.WorkspaceId);

        var response = await PostAdminCreateAsync(client, GateExchangeType, "Gate", SimulatorApiKey, SimulatorApiSecret);

        (await ReadOkAsync(response)).GetProperty("data").GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task An_Exchange_With_The_One_Account_Rule_Has_One_Account_In_A_Workspace()
    {
        await CreateExchangeAccountAsync();

        var secondGate = await PostAsync("/exchange_accounts", new { exchangeType = "GATEIO", name = "Second Gate" });
        secondGate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(secondGate)).GetProperty("message").GetString()
            .Should().Be("This workspace already has a Gate.io account. A workspace can have only one Gate.io account");
        await AssertConnectFailsAsync(HttpStatusCode.Conflict, "EXCHANGE_ACCOUNT_LIMIT_REACHED", SimulatorApiKey, SimulatorApiSecret);

        var otherExchange = await PostAsync("/exchange_accounts", new { exchangeType = "BITGET", name = "Other exchange" });
        otherExchange.StatusCode.Should().Be(HttpStatusCode.Created);
        var accounts = await ReadOkAsync(await Http.GetAsync("/exchange_accounts"));
        accounts.EnumerateArray().Count(account => account.GetProperty("type").GetString() == "GATEIO").Should().Be(1);
    }

    [Fact]
    public async Task Parallel_Creates_Of_A_One_Account_Exchange_Create_One_Account()
    {
        var calls = Enumerable.Range(0, 5).Select(index => PostAsync(
            "/exchange_accounts",
            new { exchangeType = "GATEIO", name = $"Gate {index}" }));

        var responses = await Task.WhenAll(calls);

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(4);
    }

    [Fact]
    public async Task A_Simulator_Account_Connects_In_Only_One_Workspace()
    {
        await CreateConnectedAccountAsync();
        var (otherWorkspaceId, otherApiKey) = await Fixture.CreateWorkspaceAsync("Other workspace");
        var other = Fixture.CreateAnonymousHttpClient();
        other.DefaultRequestHeaders.Add("X-API-Key", otherApiKey);
        other.DefaultRequestHeaders.Add("X-Workspace-Id", otherWorkspaceId);

        var createConnected = await PostAdminCreateAsync(other, GateExchangeType, "Gate", SimulatorApiKey, SimulatorApiSecret);
        createConnected.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(createConnected)).GetProperty("error").GetProperty("code").GetString()
            .Should().Be("EXCHANGE_ACCOUNT_ALREADY_CONNECTED");

        var created = await PostAsync(other, "/exchange_accounts", new { exchangeType = "GATEIO", name = "No connection" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var accountId = (await ReadJsonAsync(created)).GetProperty("id").GetString()!;
        var connect = await PostAdminConnectAsync(other, accountId, SimulatorApiKey, SimulatorApiSecret);
        connect.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(connect)).GetProperty("error").GetProperty("code").GetString()
            .Should().Be("EXCHANGE_ACCOUNT_ALREADY_CONNECTED");
    }

    [Fact]
    public async Task Parallel_Connections_Of_One_Simulator_Account_Create_One_Connection()
    {
        var bodies = new List<object>();
        for (var index = 0; index < 5; index++)
        {
            var creds = await EncryptCredsAsync(SimulatorApiKey, SimulatorApiSecret);
            bodies.Add(new { exchangeType = "GATEIO", name = $"Gate {index}", apiKey = SimulatorApiKey, creds });
        }

        var responses = await Task.WhenAll(bodies.Select(body => PostAsync("/admin/exchange-accounts", body)));

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(4);
    }

    [Fact]
    public async Task Secrets_Are_Not_In_The_Database_Replies_Or_Logs()
    {
        const string wrongSecretValue = "wrong-secret-value-77";
        var wrongSecret = await PostAdminCreateAsync(Http, GateExchangeType, "Wrong", SimulatorApiKey, wrongSecretValue);
        var wrongSecretText = await wrongSecret.Content.ReadAsStringAsync();
        (await ReadJsonAsync(wrongSecret)).GetProperty("error").GetProperty("code").GetString()
            .Should().Be("INVALID_EXCHANGE_CREDENTIALS");

        var fireblocksCreds = await EncryptCredsAsync(SimulatorApiKey, SimulatorApiSecret);
        var created = await PostAsync(
            "/exchange_accounts",
            new { exchangeType = "GATEIO", name = "With creds", creds = fireblocksCreds, key = SimulatorApiKey },
            "secret-check-key");
        var createdText = await created.Content.ReadAsStringAsync();
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var accountId = (await ReadJsonAsync(created)).GetProperty("id").GetString();

        var readText = await (await Http.GetAsync($"/admin/exchange-accounts/{accountId}")).Content.ReadAsStringAsync();
        readText.Should().Contain(SimulatorAccount, "the account is connected through creds");

        var secrets = new[] { SimulatorApiSecret, SimulatorApiKey, fireblocksCreds, wrongSecretValue };
        foreach (var secret in secrets)
        {
            createdText.Should().NotContain(secret);
            wrongSecretText.Should().NotContain(secret);
            readText.Should().NotContain(secret);
            (await CountDatabaseRowsContainingAsync(secret)).Should().Be(0, $"the database must not hold {secret}");
        }

        await WaitUntilAsync(
            () => Task.FromResult(Logs.AllText.Contains("creds\\\":\\\"[redacted]")),
            "the add call is logged with its creds removed");
        foreach (var secret in secrets)
        {
            Logs.AllText.Should().NotContain(secret);
        }

        Logs.AllText.Should().Contain("apiKey\\\":\\\"[redacted]");
        Logs.AllText.Should().Contain("creds\\\":\\\"[redacted]");
        Logs.AllText.Should().Contain("key\\\":\\\"[redacted]");
    }

    [Fact]
    public async Task A_Body_That_Is_Not_Json_Is_Not_Logged_On_A_Credential_Route()
    {
        const string secretText = "not-json-secret-value-3391";
        using var content = new StringContent($"{{ \"apiSecret\": \"{secretText}\"", System.Text.Encoding.UTF8, "application/json");

        var response = await Http.PostAsync("/admin/exchange-accounts", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WaitUntilAsync(() => Task.FromResult(Logs.AllText.Contains("[body redacted]")), "the redacted body is logged");
        Logs.AllText.Should().NotContain(secretText);
    }

    [Fact]
    public async Task Request_Bodies_Of_Other_Routes_Are_Logged()
    {
        await Fixture.AdminClient.CreateVaultAsync("Logged vault name");

        await WaitUntilAsync(
            () => Task.FromResult(Logs.AllText.Contains("Logged vault name")),
            "the request body is logged");
    }

    [Fact]
    public async Task Balances_Without_A_Connection_Match_The_Stored_Values()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");
        await PatchAssetAsync(accountId, "BTC", new { exchangeHold = "0.2", credit = "0.7" });
        await SubmitRemoveAsync(vaultId, accountId, "0.3");

        var asset = await GetExchangeAssetAsync(accountId);

        Amount(asset, "balance").Should().Be(0.9999m);
        Amount(asset, "total").Should().Be(0.9999m);
        Amount(asset, "lockedAmount").Should().Be(0.5m);
        Amount(asset, "available").Should().Be(0.4999m);
        Amount(asset, "credit").Should().Be(0.7m);
        foreach (var field in new[] { "id", "assetLegacyId", "assetSymbol", "providerSymbol" })
        {
            asset.GetProperty(field).GetString().Should().Be("BTC");
        }

        asset.TryGetProperty("assetId", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Available_Is_Not_Below_Zero_And_Amounts_Have_No_Exponent()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "0.00019");
        await PatchAssetAsync(accountId, "BTC", new { exchangeHold = "1" });

        var asset = await GetExchangeAssetAsync(accountId);

        asset.GetProperty("total").GetString().Should().Be("0.00009");
        asset.GetProperty("available").GetString().Should().Be("0");
    }

    [Fact]
    public async Task Known_Asset_With_No_Row_Returns_Zero_And_Unknown_Values_Get_404()
    {
        var accountId = await CreateExchangeAccountAsync();

        var asset = await GetExchangeAssetAsync(accountId, "ETH");
        foreach (var field in new[] { "balance", "total", "lockedAmount", "available", "credit" })
        {
            asset.GetProperty(field).GetString().Should().Be("0");
        }

        (await Http.GetAsync($"/exchange_accounts/{accountId}/NOPE")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Http.GetAsync($"/exchange_accounts/{Guid.NewGuid()}/BTC")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Balances_With_A_Connection_Use_The_Simulator()
    {
        var accountId = await CreateConnectedAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");
        await WaitUntilAsync(
            () => Task.FromResult(Simulator.GetSpotAvailable(SimulatorAccount, "BTC") == 0.9999m),
            "the add reaches the simulator");

        Simulator.SetBalance(SimulatorAccount, "BTC", spotAvailable: 0.6m, spotLocked: 0.25m);
        var limitedBySimulator = await GetExchangeAssetAsync(accountId);
        Amount(limitedBySimulator, "total").Should().Be(0.9999m);
        Amount(limitedBySimulator, "available").Should().Be(0.6m);
        Amount(limitedBySimulator, "credit").Should().Be(0.85m);

        Simulator.SetBalance(SimulatorAccount, "BTC", spotAvailable: 5m);
        var limitedByCustody = await GetExchangeAssetAsync(accountId);
        Amount(limitedByCustody, "available").Should().Be(0.9999m);
        Amount(limitedByCustody, "credit").Should().Be(5m);

        var account = await ReadOkAsync(await Http.GetAsync($"/exchange_accounts/{accountId}"));
        account.GetProperty("success").GetBoolean().Should().BeTrue();
        account.GetProperty("assets").EnumerateArray().Select(asset => asset.GetProperty("id").GetString())
            .Should().Equal("BTC", "USDT_ERC20");
    }

    [Fact]
    public async Task Gate_Usdt_Balance_Shows_On_The_Waterblocks_Usdt_Asset()
    {
        var accountId = await CreateConnectedAccountAsync();
        Simulator.SetBalance(SimulatorAccount, "USDT", spotAvailable: 250m, spotLocked: 50m);

        var asset = await GetExchangeAssetAsync(accountId, "USDT_ERC20");

        Amount(asset, "credit").Should().Be(300m);
        (await Http.GetAsync($"/exchange_accounts/{accountId}/USDT")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unreadable_Simulator_Sets_Success_False_And_Available_Zero()
    {
        var accountId = await CreateConnectedAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");

        Simulator.FailAccountReads = true;

        var account = await ReadOkAsync(await Http.GetAsync($"/exchange_accounts/{accountId}"));
        account.GetProperty("success").GetBoolean().Should().BeFalse();
        var asset = account.GetProperty("assets").EnumerateArray().Single(item => item.GetProperty("id").GetString() == "BTC");
        Amount(asset, "available").Should().Be(0m);
        Amount(asset, "credit").Should().Be(0.9999m);
        Amount(asset, "total").Should().Be(0.9999m);
    }

    [Fact]
    public async Task Credit_Can_Be_Set_Only_Without_A_Connection()
    {
        var connectedId = await CreateConnectedAccountAsync();
        var refused = await SendPatchAsync(connectedId, "BTC", new { credit = "1" });
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(refused)).GetProperty("error").GetProperty("code").GetString().Should().Be("CREDIT_READ_ONLY");

        var hold = await SendPatchAsync(connectedId, "BTC", new { exchangeHold = "1" });
        hold.StatusCode.Should().Be(HttpStatusCode.OK);
        Amount((await ReadJsonAsync(hold)).GetProperty("data"), "exchangeHold").Should().Be(1m);
    }

    [Fact]
    public async Task Collateral_Account_Shows_The_Main_Account_Assets_With_Its_Own_Id()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");

        var main = await ReadOkAsync(await Http.GetAsync($"/exchange_accounts/{accountId}"));
        var collateral = await ReadOkAsync(await Http.GetAsync($"/off_exchange/collateral_accounts/{accountId}"));
        var repeated = await ReadOkAsync(await Http.GetAsync($"/off_exchange/collateral_accounts/{accountId}"));

        collateral.GetProperty("id").GetString().Should().NotBe(accountId);
        collateral.GetProperty("id").GetString().Should().Be(repeated.GetProperty("id").GetString());
        collateral.GetProperty("mainAccountId").GetString().Should().Be(accountId);
        collateral.GetProperty("assets").GetRawText().Should().Be(main.GetProperty("assets").GetRawText());
    }

    private async Task AssertConnectFailsAsync(HttpStatusCode status, string code, string apiKey, string apiSecret)
    {
        var response = await PostAdminCreateAsync(Http, GateExchangeType, "Gate", apiKey, apiSecret);
        response.StatusCode.Should().Be(status);
        (await ReadJsonAsync(response)).GetProperty("error").GetProperty("code").GetString().Should().Be(code);
    }

    private async Task AssertFireblocksAddFailsAsync(string creds, string key, string message)
    {
        var response = await PostAsync("/exchange_accounts", new { exchangeType = GateExchangeType, name = "Refused", key, creds });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString().Should().Be(message);
    }

    private Task<HttpResponseMessage> SendPatchAsync(string accountId, string assetId, object body)
    {
        return Http.PatchAsJsonAsync($"/admin/exchange-accounts/{accountId}/assets/{assetId}", body);
    }

    private async Task PatchAssetAsync(string accountId, string assetId, object body)
    {
        await ReadOkAsync(await SendPatchAsync(accountId, assetId, body));
    }

    private static HttpRequestMessage CreateRequest(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { });
        }

        return request;
    }

    private async Task<int> CountDatabaseRowsContainingAsync(string text)
    {
        var database = Fixture.GetDbContext();
        var tables = await database.Database
            .SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'")
            .ToListAsync();

        var total = 0;
        foreach (var table in tables)
        {
#pragma warning disable EF1002 // The table names come from the database catalog.
            total += await database.Database
                .SqlQueryRaw<int>($"SELECT COUNT(*)::int AS \"Value\" FROM \"{table}\" AS item WHERE item::text LIKE {{0}}", $"%{text}%")
                .SingleAsync();
#pragma warning restore EF1002
        }

        return total;
    }
}
