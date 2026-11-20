using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class OffExchangeIdempotencyTests : OffExchangeTestBase
{
    [Fact]
    public async Task Repeated_Add_Returns_The_First_Reply_And_Creates_One_Transaction()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        var body = AddBody(vaultId, accountId, "0.5");

        var first = await PostAsync("/off_exchange/add", body, "add-key-1");
        var firstText = await first.Content.ReadAsStringAsync();
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await PostAsync("/off_exchange/add", body, "add-key-1");
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadAsStringAsync()).Should().Be(firstText);

        await AssertTransactionCountAsync(accountId, 1);
        (await GetVaultBalanceAsync(vaultId)).Pending.Should().Be(0.5m);
    }

    [Fact]
    public async Task Repeated_Remove_Returns_The_First_Reply_And_Locks_Once()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");
        var body = RemoveBody(vaultId, accountId, "0.4");

        var first = await PostAsync("/off_exchange/remove", body, "remove-key-1");
        var firstText = await first.Content.ReadAsStringAsync();
        var second = await PostAsync("/off_exchange/remove", body, "remove-key-1");

        (await second.Content.ReadAsStringAsync()).Should().Be(firstText);
        await AssertTransactionCountAsync(accountId, 2);
        Amount(await GetExchangeAssetAsync(accountId), "lockedAmount").Should().Be(0.4m);
    }

    [Fact]
    public async Task Repeated_Account_Creation_Returns_The_First_Reply()
    {
        var body = new { exchangeType = "GATEIO", name = "Gate once" };

        var first = await PostAsync("/exchange_accounts", body, "account-key-1");
        var firstText = await first.Content.ReadAsStringAsync();
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await PostAsync("/exchange_accounts", body, "account-key-1");
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        (await second.Content.ReadAsStringAsync()).Should().Be(firstText);

        var list = await ReadOkAsync(await Http.GetAsync("/exchange_accounts"));
        list.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task First_Error_Reply_Is_Returned_And_The_Operation_Does_Not_Run_Again()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync(amount: 0.1m);
        var body = AddBody(vaultId, accountId, "0.5");

        var first = await PostAsync("/off_exchange/add", body, "error-key-1");
        first.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var firstText = await first.Content.ReadAsStringAsync();

        var vault = await Fixture.AdminClient.GetVaultAsync(vaultId);
        await Fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            DestinationAddress = vault.Data!.Wallets.Single().DepositAddress,
            Amount = "5",
        });

        var second = await PostAsync("/off_exchange/add", body, "error-key-1");

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.Content.ReadAsStringAsync()).Should().Be(firstText);
        await AssertTransactionCountAsync(accountId, 0);
    }

    [Fact]
    public async Task Parallel_Calls_With_One_Key_Create_One_Transaction()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        var body = AddBody(vaultId, accountId, "0.5");

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => PostAsync("/off_exchange/add", body, "parallel-key-1")));
        var replies = await Task.WhenAll(responses.Select(response => response.Content.ReadAsStringAsync()));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
        replies.Distinct().Should().HaveCount(1);
        await AssertTransactionCountAsync(accountId, 1);
        (await GetVaultBalanceAsync(vaultId)).Pending.Should().Be(0.5m);
    }

    [Fact]
    public async Task The_Record_Survives_A_Restart()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        var body = AddBody(vaultId, accountId, "0.5");
        var first = await PostAsync("/off_exchange/add", body, "restart-key-1");
        var firstText = await first.Content.ReadAsStringAsync();

        await using var restarted = Fixture.CreateFactory(SimulatorSettings(), ConfigureServices);
        var client = restarted.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", Fixture.ApiKey);

        var second = await PostAsync(client, "/off_exchange/add", body, "restart-key-1");

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadAsStringAsync()).Should().Be(firstText);
        await AssertTransactionCountAsync(accountId, 1);
    }

    [Fact]
    public async Task Expired_Key_Runs_Again_And_The_External_Id_Gives_1438()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        var body = AddBody(vaultId, accountId, "0.5", externalTxId: Guid.NewGuid().ToString());
        (await PostAsync("/off_exchange/add", body, "expired-key-1")).StatusCode.Should().Be(HttpStatusCode.OK);

        var database = Fixture.GetDbContext();
        await database.IdempotencyRecords
            .Where(record => record.Key == "expired-key-1")
            .ExecuteUpdateAsync(update => update.SetProperty(
                record => record.ExpiresAt,
                DateTimeOffset.UtcNow.AddSeconds(-1)));

        var second = await PostAsync("/off_exchange/add", body, "expired-key-1");

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(second)).GetProperty("code").GetDecimal().Should().Be(1438);
        await AssertTransactionCountAsync(accountId, 1);
    }

    [Fact]
    public async Task Key_Longer_Than_40_Characters_Gets_400_And_No_Record()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();

        var refused = await PostAsync("/off_exchange/add", AddBody(vaultId, accountId, "0.5"), new string('k', 41));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertTransactionCountAsync(accountId, 0);
        var database = Fixture.GetDbContext();
        (await database.IdempotencyRecords.CountAsync()).Should().Be(0);

        var accepted = await PostAsync("/off_exchange/add", AddBody(vaultId, accountId, "0.5"), new string('k', 40));
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Failed_Authentication_Creates_No_Record()
    {
        var anonymous = Fixture.CreateAnonymousHttpClient();

        var response = await PostAsync(anonymous, "/off_exchange/add", new { }, "no-auth-key");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var database = Fixture.GetDbContext();
        (await database.IdempotencyRecords.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task One_Key_Is_Separate_For_Each_Route_And_Workspace()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");
        const string key = "shared-key-1";

        var add = await PostAsync("/off_exchange/add", AddBody(vaultId, accountId, "0.1"), key);
        var remove = await PostAsync("/off_exchange/remove", RemoveBody(vaultId, accountId, "0.1"), key);
        (await ReadJsonAsync(add)).GetProperty("id").GetString()
            .Should().NotBe((await ReadJsonAsync(remove)).GetProperty("id").GetString());

        var (_, otherApiKey) = await Fixture.CreateWorkspaceAsync("Other workspace");
        var other = Fixture.CreateAnonymousHttpClient();
        other.DefaultRequestHeaders.Add("X-API-Key", otherApiKey);
        var otherReply = await PostAsync(other, "/off_exchange/add", AddBody(vaultId, accountId, "0.1"), key);
        otherReply.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Transaction_Does_Not_Use_The_Idempotency_Key()
    {
        var vaultId = await CreateFundedVaultAsync();
        var body = new
        {
            assetId = "BTC",
            amount = "0.1",
            source = new { type = "VAULT_ACCOUNT", id = vaultId },
            destination = new
            {
                type = "ONE_TIME_ADDRESS",
                oneTimeAddress = new { address = "bc1qexternaldestination000000000000000000" },
            },
        };

        var first = await ReadOkAsync(await PostAsync("/transactions", body, "transactions-key"));
        var second = await ReadOkAsync(await PostAsync("/transactions", body, "transactions-key"));

        first.GetProperty("id").GetString().Should().NotBe(second.GetProperty("id").GetString());
    }

    private async Task AssertTransactionCountAsync(string accountId, int expected)
    {
        var database = Fixture.GetDbContext();
        (await database.Transactions.CountAsync(item => item.ExchangeAccountId == accountId)).Should().Be(expected);
    }
}
