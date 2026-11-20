using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Models;
using Waterblocks.Api.Utils;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class OffExchangeCollateralTests : OffExchangeTestBase
{
    private static readonly string LargestReadableAmount =
        DecimalColumnGuard.MaxReadableValue.ToString(CultureInfo.InvariantCulture);
    private static readonly string CollateralTotalTooLarge =
        $"The BTC collateral total is too large. Maximum supported value is {LargestReadableAmount}.";

    [Theory]
    [InlineData(true, 0.5, 0.4999)]
    [InlineData(false, 0.5001, 0.5)]
    public async Task Add_Collateral_Moves_The_Expected_Amounts(bool gross, decimal vaultFall, decimal custodyRise)
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();

        var transactionId = await SubmitAddAsync(vaultId, accountId, "0.5", gross);

        var reserved = await GetVaultBalanceAsync(vaultId);
        reserved.Balance.Should().Be(2m);
        reserved.Pending.Should().Be(vaultFall);
        var beforeCompletion = await GetExchangeAssetAsync(accountId);
        Amount(beforeCompletion, "total").Should().Be(0m);
        Amount(beforeCompletion, "credit").Should().Be(0m);

        await CompleteAsync(transactionId);

        var settled = await GetVaultBalanceAsync(vaultId);
        settled.Balance.Should().Be(2m - vaultFall);
        settled.Pending.Should().Be(0m);
        var asset = await GetExchangeAssetAsync(accountId);
        Amount(asset, "total").Should().Be(custodyRise);
        Amount(asset, "credit").Should().Be(custodyRise);
        Amount(asset, "available").Should().Be(custodyRise);
    }

    [Theory]
    [InlineData(true, 0.4999)]
    [InlineData(false, 0.5)]
    public async Task Add_Collateral_Transaction_Fields_Match_The_Contract(bool gross, decimal netAmount)
    {
        var accountId = await CreateExchangeAccountAsync("Gate fields");
        var vaultId = await CreateFundedVaultAsync(name: "Fields vault");
        var externalTxId = Guid.NewGuid().ToString();
        var created = await PostAsync(
            "/off_exchange/add",
            AddBody(vaultId, accountId, "0.5", gross, externalTxId: externalTxId, note: "LL add"));
        var transactionId = (await ReadOkAsync(created)).GetProperty("id").GetString()!;
        transactionId.Should().StartWith($"{Fixture.WorkspaceId}::");
        await CompleteAsync(transactionId);

        var transaction = await GetTransactionAsync(transactionId);

        var source = transaction.GetProperty("source");
        source.GetProperty("type").GetString().Should().Be("VAULT_ACCOUNT");
        source.GetProperty("id").GetString().Should().Be(vaultId);
        source.GetProperty("name").GetString().Should().Be("Fields vault");
        var destination = transaction.GetProperty("destination");
        destination.GetProperty("type").GetString().Should().Be("EXCHANGE_ACCOUNT");
        destination.GetProperty("id").GetString().Should().Be(accountId);
        destination.GetProperty("name").GetString().Should().Be("Gate fields");
        destination.GetProperty("subType").GetString().Should().Be("GATEIO");

        var admin = (await ReadOkAsync(await Http.GetAsync($"/admin/exchange-accounts/{accountId}"))).GetProperty("data");
        var collateralAddress = admin.GetProperty("assets")[0].GetProperty("collateralAddress").GetString();
        transaction.GetProperty("destinationAddress").GetString().Should().Be(collateralAddress);
        AssertAmounts(transaction, netAmount, requestedAmount: 0.5m);
        transaction.GetProperty("note").GetString().Should().Be("LL add");
        transaction.GetProperty("externalTxId").GetString().Should().Be(externalTxId);

        var byExternalId = await ReadOkAsync(await Http.GetAsync($"/transactions/external_tx_id/{externalTxId}"));
        byExternalId.GetProperty("destination").GetProperty("id").GetString().Should().Be(accountId);

        var database = Fixture.GetDbContext();
        (await database.Addresses.AnyAsync(address => address.AddressValue == collateralAddress))
            .Should().BeFalse("the collateral address must not look like a vault address");
    }

    [Theory]
    [InlineData(true, 0.4, 0.3999)]
    [InlineData(false, 0.4001, 0.4)]
    public async Task Remove_Collateral_Moves_The_Expected_Amounts(bool gross, decimal custodyFall, decimal vaultRise)
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");
        var vaultBefore = (await GetVaultBalanceAsync(vaultId)).Balance;

        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4", gross);
        removal.GetProperty("status").GetString().Should().Be("SUBMITTED");
        var transactionId = removal.GetProperty("id").GetString()!;

        var locked = await GetExchangeAssetAsync(accountId);
        Amount(locked, "total").Should().Be(0.9999m);
        Amount(locked, "lockedAmount").Should().Be(custodyFall);
        Amount(locked, "available").Should().Be(0.9999m - custodyFall);
        (await GetVaultBalanceAsync(vaultId)).Balance.Should().Be(vaultBefore);

        await CompleteAsync(transactionId);

        var settled = await GetExchangeAssetAsync(accountId);
        Amount(settled, "total").Should().Be(0.9999m - custodyFall);
        Amount(settled, "credit").Should().Be(0.9999m - custodyFall);
        Amount(settled, "lockedAmount").Should().Be(0m);
        var vault = await GetVaultBalanceAsync(vaultId);
        vault.Balance.Should().Be(vaultBefore + vaultRise);
        vault.Pending.Should().Be(0m);

        var transaction = await GetTransactionAsync(transactionId);
        transaction.GetProperty("source").GetProperty("type").GetString().Should().Be("EXCHANGE_ACCOUNT");
        transaction.GetProperty("source").GetProperty("id").GetString().Should().Be(accountId);
        transaction.GetProperty("source").GetProperty("subType").GetString().Should().Be("GATEIO");
        transaction.GetProperty("destination").GetProperty("type").GetString().Should().Be("VAULT_ACCOUNT");
        transaction.GetProperty("destination").GetProperty("id").GetString().Should().Be(vaultId);
        AssertAmounts(transaction, vaultRise, requestedAmount: 0.4m);
    }

    [Fact]
    public async Task Remove_Pays_The_First_Address_Of_The_Vault_Address_List()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddDepositAddressesAsync(vaultId, ("bc1qremovetarget0002", 2), ("bc1qremovetarget0001", 1));
        await AddAndCompleteAsync(vaultId, accountId, "1");

        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");

        var addresses = await ReadOkAsync(await Http.GetAsync($"/vault/accounts/{vaultId}/BTC/addresses_paginated?count=50"));
        var firstListed = addresses.GetProperty("addresses")[0].GetProperty("address").GetString();
        firstListed.Should().Be("bc1qremovetarget0001");
        var transaction = await GetTransactionAsync(removal.GetProperty("id").GetString()!);
        transaction.GetProperty("destinationAddress").GetString().Should().Be(firstListed);
    }

    [Fact]
    public async Task Stored_Credit_Does_Not_Fall_Below_Zero()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");
        await ReadOkAsync(await Http.PatchAsJsonAsync(
            $"/admin/exchange-accounts/{accountId}/assets/BTC",
            new { credit = "0.1" }));

        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");
        await CompleteAsync(removal.GetProperty("id").GetString()!);

        Amount(await GetExchangeAssetAsync(accountId), "credit").Should().Be(0m);
    }

    [Fact]
    public async Task Poller_Query_Finds_Completed_Collateral_Transfers()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        var before = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds();
        var addId = await AddAndCompleteAsync(vaultId, accountId, "1");

        var page = await ReadOkAsync(await Http.GetAsync($"/transactions?status=COMPLETED&limit=200&after={before}"));

        var transaction = page.EnumerateArray().Single(item => item.GetProperty("id").GetString() == addId);
        transaction.GetProperty("createdAt").ValueKind.Should().Be(JsonValueKind.Number);
        transaction.GetProperty("amountInfo").GetProperty("netAmount").GetString().Should().Be("0.9999");
        var createdAt = transaction.GetProperty("createdAt").GetDecimal();
        var older = await ReadOkAsync(await Http.GetAsync(
            $"/transactions?status=COMPLETED&limit=200&after={before}&before={createdAt}"));
        older.EnumerateArray().Should().NotContain(item => item.GetProperty("id").GetString() == addId);
    }

    [Fact]
    public async Task Add_Above_The_Vault_Balance_Gets_400_And_Changes_Nothing()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync(amount: 1m);

        var response = await PostAsync("/off_exchange/add", AddBody(vaultId, accountId, "1", gross: false));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString().Should().StartWith("Insufficient balance");
        var vault = await GetVaultBalanceAsync(vaultId);
        vault.Balance.Should().Be(1m);
        vault.Pending.Should().Be(0m);
        Amount(await GetExchangeAssetAsync(accountId), "total").Should().Be(0m);
        var database = Fixture.GetDbContext();
        (await database.Transactions.CountAsync(item => item.ExchangeAccountId == accountId)).Should().Be(0);
    }

    [Fact]
    public async Task Parallel_Adds_Cannot_Reserve_More_Than_The_Vault_Balance()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => PostAsync("/off_exchange/add", AddBody(vaultId, accountId, "0.5"))));

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(4);
        responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest).Should().Be(6);
        (await GetVaultBalanceAsync(vaultId)).Pending.Should().Be(2m);
    }

    [Fact]
    public async Task Add_Above_The_Largest_Readable_Collateral_Total_Gets_400_And_Changes_Nothing()
    {
        var accountId = await CreateExchangeAccountAsync();
        var largeVaultId = await CreateFundedVaultAsync(amount: DecimalColumnGuard.MaxReadableValue, name: "Large vault");
        var smallVaultId = await CreateFundedVaultAsync(amount: 1m, name: "Small vault");
        await AddAndCompleteAsync(largeVaultId, accountId, LargestReadableAmount);

        var response = await PostAsync("/off_exchange/add", AddBody(smallVaultId, accountId, "1"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString().Should().Be(CollateralTotalTooLarge);
        Amount(await GetExchangeAssetAsync(accountId), "total").Should().Be(DecimalColumnGuard.MaxReadableValue - BtcFee);
        var smallVault = await GetVaultBalanceAsync(smallVaultId);
        smallVault.Balance.Should().Be(1m);
        smallVault.Pending.Should().Be(0m);
    }

    [Fact]
    public async Task Add_Cannot_Complete_When_An_Earlier_Add_Filled_The_Collateral_Total()
    {
        var accountId = await CreateExchangeAccountAsync();
        var largeVaultId = await CreateFundedVaultAsync(amount: DecimalColumnGuard.MaxReadableValue, name: "Large vault");
        var smallVaultId = await CreateFundedVaultAsync(amount: 1m, name: "Small vault");
        var largeAddId = await SubmitAddAsync(largeVaultId, accountId, LargestReadableAmount);
        var smallAddId = await SubmitAddAsync(smallVaultId, accountId, "1");
        await CompleteAsync(largeAddId);
        (await Fixture.AdminClient.ApproveTransactionAsync(smallAddId)).IsSuccess.Should().BeTrue();
        (await Fixture.AdminClient.SignTransactionAsync(smallAddId)).IsSuccess.Should().BeTrue();
        (await Fixture.AdminClient.BroadcastTransactionAsync(smallAddId)).IsSuccess.Should().BeTrue();
        (await Fixture.AdminClient.ConfirmTransactionAsync(smallAddId)).IsSuccess.Should().BeTrue();

        var completion = await Http.PostAsync($"/admin/transactions/{smallAddId}/complete", null);

        completion.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(completion)).GetProperty("message").GetString().Should().Be(CollateralTotalTooLarge);
        (await GetTransactionAsync(smallAddId)).GetProperty("status").GetString().Should().Be("CONFIRMING");
        Amount(await GetExchangeAssetAsync(accountId), "total").Should().Be(DecimalColumnGuard.MaxReadableValue - BtcFee);
        var reserved = await GetVaultBalanceAsync(smallVaultId);
        reserved.Balance.Should().Be(1m);
        reserved.Pending.Should().Be(1m);

        (await Fixture.AdminClient.FailTransactionAsync(smallAddId, "collateral total too large")).IsSuccess.Should().BeTrue();
        var released = await GetVaultBalanceAsync(smallVaultId);
        released.Balance.Should().Be(1m);
        released.Pending.Should().Be(0m);
    }

    [Fact]
    public async Task Wrong_Peer_Types_Get_400()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();

        var addFromExchange = await PostAsync("/off_exchange/add", RemoveBody(vaultId, accountId, "0.1"));
        addFromExchange.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var removeFromVault = await PostAsync("/off_exchange/remove", AddBody(vaultId, accountId, "0.1"));
        removeFromVault.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var missingRequest = await PostAsync("/off_exchange/add", new { isSrcCollateral = false });
        missingRequest.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("\"yes\"")]
    [InlineData("1")]
    [InlineData("null")]
    public async Task Collateral_Flags_Accept_Any_Value(string flag)
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        var body = $$"""
            {
              "transactionRequest": {
                "assetId": "BTC",
                "source": {"type": "VAULT_ACCOUNT", "id": "{{vaultId}}", "isCollateral": {{flag}}},
                "destination": {"type": "EXCHANGE_ACCOUNT", "id": "{{accountId}}", "isCollateral": {{flag}}},
                "amount": "0.1"
              },
              "isSrcCollateral": {{flag}}
            }
            """;

        var response = await Http.PostAsync(
            "/off_exchange/add",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        (await ReadOkAsync(response)).GetProperty("status").GetString().Should().Be("SUBMITTED");
    }

    [Fact]
    public async Task Vault_And_Account_Of_Another_Workspace_Get_404()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        var (otherWorkspaceId, otherApiKey) = await Fixture.CreateWorkspaceAsync("Other workspace");
        var other = Fixture.CreateAnonymousHttpClient();
        other.DefaultRequestHeaders.Add("X-API-Key", otherApiKey);
        other.DefaultRequestHeaders.Add("X-Workspace-Id", otherWorkspaceId);
        var otherVault = await Fixture.CreateAdminClientForWorkspace(otherWorkspaceId).CreateVaultAsync("Other vault");
        var otherVaultId = otherVault.Data!.Id;

        (await PostAsync(other, "/off_exchange/add", AddBody(otherVaultId, accountId, "0.1")))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PostAsync("/off_exchange/add", AddBody(otherVaultId, accountId, "0.1")))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PostAsync(other, "/off_exchange/remove", RemoveBody(vaultId, accountId, "0.1")))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Connected_Account_Accepts_Only_The_Assets_Of_Its_Exchange()
    {
        var accountId = await CreateConnectedAccountAsync();
        var vaultId = await CreateFundedVaultAsync("ETH", 1m);

        var response = await PostAsync("/off_exchange/add", AddBody(vaultId, accountId, "0.1", assetId: "ETH"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString()
            .Should().Be("A connected Gate.io account does not accept ETH");
    }

    [Fact]
    public async Task Removal_Needs_An_Asset_That_Pays_Its_Own_Fee()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync("USDC", 10m);

        var response = await PostAsync("/off_exchange/remove", RemoveBody(vaultId, accountId, "1", assetId: "USDC"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("VAULT_ACCOUNT", "EXCHANGE_ACCOUNT")]
    [InlineData("EXCHANGE_ACCOUNT", "VAULT_ACCOUNT")]
    public async Task Create_Transaction_Refuses_Exchange_Peers(string sourceType, string destinationType)
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();

        var response = await PostAsync("/transactions", new
        {
            assetId = "BTC",
            amount = "0.1",
            source = new { type = sourceType, id = sourceType == "VAULT_ACCOUNT" ? vaultId : accountId },
            destination = new { type = destinationType, id = destinationType == "VAULT_ACCOUNT" ? vaultId : accountId },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString().Should().Contain("/off_exchange");
    }

    [Fact]
    public async Task Removal_Above_Free_Is_Cancelled_And_Changes_No_Balance()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");
        var vaultBefore = await GetVaultBalanceAsync(vaultId);

        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.9999", gross: false);

        removal.GetProperty("status").GetString().Should().Be("CANCELLED");
        var removalId = removal.GetProperty("id").GetString()!;
        await AssertRejectedRemovalAsync(removalId);
        (await GetTransferAsync(accountId, removalId)).GetProperty("failureReason").GetString().Should().Be(
            "The removal needs 1 BTC from the collateral account (0.9999 BTC plus the 0.0001 BTC network fee), but only 0.9999 BTC is free.");
        var asset = await GetExchangeAssetAsync(accountId);
        Amount(asset, "total").Should().Be(0.9999m);
        Amount(asset, "lockedAmount").Should().Be(0m);
        (await GetVaultBalanceAsync(vaultId)).Should().Be(vaultBefore);

        var withFeeDeducted = await SubmitRemoveAsync(vaultId, accountId, "0.9999", gross: true);
        withFeeDeducted.GetProperty("status").GetString().Should().Be("SUBMITTED");
    }

    [Fact]
    public async Task Removal_Above_Free_Because_Of_Exchange_Hold_Is_Cancelled()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");
        await ReadOkAsync(await Http.PatchAsJsonAsync(
            $"/admin/exchange-accounts/{accountId}/assets/BTC",
            new { exchangeHold = "0.8" }));

        var refused = await SubmitRemoveAsync(vaultId, accountId, "0.3");
        refused.GetProperty("status").GetString().Should().Be("CANCELLED");
        var refusedId = refused.GetProperty("id").GetString()!;
        await AssertRejectedRemovalAsync(refusedId);
        (await GetTransferAsync(accountId, refusedId)).GetProperty("failureReason").GetString().Should().Be(
            "The removal needs 0.3 BTC from the collateral account, but only 0.1999 BTC is free.");

        var accepted = await SubmitRemoveAsync(vaultId, accountId, "0.1");
        accepted.GetProperty("status").GetString().Should().Be("SUBMITTED");
    }

    [Fact]
    public async Task Parallel_Removals_Cannot_Lock_More_Than_Free()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");

        var calls = Enumerable.Range(0, 12)
            .Select(_ => PostAsync("/off_exchange/remove", RemoveBody(vaultId, accountId, "0.3")));
        var bodies = await Task.WhenAll((await Task.WhenAll(calls)).Select(ReadOkAsync));

        bodies.Count(body => body.GetProperty("status").GetString() == "SUBMITTED").Should().Be(3);
        bodies.Count(body => body.GetProperty("status").GetString() == "CANCELLED").Should().Be(9);
        var asset = await GetExchangeAssetAsync(accountId);
        Amount(asset, "lockedAmount").Should().Be(0.9m);
        Amount(asset, "available").Should().Be(0.0999m);
    }

    [Theory]
    [InlineData("fail")]
    [InlineData("reject")]
    [InlineData("cancel")]
    [InlineData("timeout")]
    [InlineData("fireblocks-cancel")]
    public async Task Failure_Paths_Release_The_Vault_Reservation_Of_An_Add(string failurePath)
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        var transactionId = await SubmitAddAsync(vaultId, accountId, "0.5");
        (await GetVaultBalanceAsync(vaultId)).Pending.Should().Be(0.5m);

        await RunFailurePathAsync(failurePath, transactionId);

        var vault = await GetVaultBalanceAsync(vaultId);
        vault.Balance.Should().Be(2m);
        vault.Pending.Should().Be(0m);
        Amount(await GetExchangeAssetAsync(accountId), "total").Should().Be(0m);
    }

    [Theory]
    [InlineData("fail")]
    [InlineData("reject")]
    [InlineData("cancel")]
    [InlineData("timeout")]
    [InlineData("fireblocks-cancel")]
    public async Task Failure_Paths_Release_The_Lock_Of_A_Removal(string failurePath)
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");
        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");
        Amount(await GetExchangeAssetAsync(accountId), "lockedAmount").Should().Be(0.4m);

        await RunFailurePathAsync(failurePath, removal.GetProperty("id").GetString()!);

        var asset = await GetExchangeAssetAsync(accountId);
        Amount(asset, "total").Should().Be(0.9999m);
        Amount(asset, "lockedAmount").Should().Be(0m);
        Amount(asset, "available").Should().Be(0.9999m);
    }

    [Fact]
    public async Task A_Second_Failure_Step_Does_Not_Release_Funds_Twice()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");
        var first = (await SubmitRemoveAsync(vaultId, accountId, "0.2")).GetProperty("id").GetString()!;
        await SubmitRemoveAsync(vaultId, accountId, "0.3");

        (await Fixture.AdminClient.CancelTransactionAsync(first)).IsSuccess.Should().BeTrue();
        (await Fixture.AdminClient.CancelTransactionAsync(first)).IsSuccess.Should().BeTrue();
        (await Fixture.AdminClient.FailTransactionAsync(first, "late")).IsSuccess.Should().BeFalse();

        Amount(await GetExchangeAssetAsync(accountId), "lockedAmount").Should().Be(0.3m);
    }

    [Fact]
    public async Task Fireblocks_Cancel_Releases_A_Standard_Transaction_Reservation()
    {
        var vaultId = await CreateFundedVaultAsync();
        var created = await Fixture.FireblocksClient.CreateTransactionAsync(new FireblocksCreateTransactionRequest
        {
            AssetId = "BTC",
            Amount = "0.5",
            Source = new FireblocksTransferPeerPath { Id = vaultId },
            Destination = new FireblocksDestinationTransferPeerPath
            {
                OneTimeAddress = new FireblocksOneTimeAddress { Address = "bc1qexternaldestination000000000000000000" },
            },
        });
        (await GetVaultBalanceAsync(vaultId)).Pending.Should().Be(0.5001m);

        var cancelled = await Http.PostAsync($"/transactions/{created!.Id}/cancel", null);

        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetVaultBalanceAsync(vaultId)).Pending.Should().Be(0m);
        var transaction = await GetTransactionAsync(created.Id);
        transaction.GetProperty("status").GetString().Should().Be("CANCELLED");
        transaction.GetProperty("subStatus").GetString().Should().Be("CANCELLED_BY_USER");
    }

    [Fact]
    public async Task Drop_Is_Refused_For_A_Collateral_Transfer()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync("ETH", 1m);
        var added = await PostAsync("/off_exchange/add", AddBody(vaultId, accountId, "0.1", assetId: "ETH"));
        var transactionId = (await ReadOkAsync(added)).GetProperty("id").GetString();

        var response = await Http.PostAsJsonAsync($"/transactions/{transactionId}/drop", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_Calls_Use_The_Same_Rules_And_Record_The_User()
    {
        var accountId = await CreateExchangeAccountAsync("Gate admin");
        var vaultId = await CreateFundedVaultAsync(name: "Admin vault");

        var added = await PostAsync($"/admin/exchange-accounts/{accountId}/add-collateral", new
        {
            assetId = "BTC",
            vaultAccountId = vaultId,
            amount = "1",
            treatAsGrossAmount = true,
            note = "From the screen",
            initiatedBy = "tester@example.com",
        });

        var addTransfer = (await ReadOkAsync(added)).GetProperty("data");
        addTransfer.GetProperty("direction").GetString().Should().Be("ADD");
        addTransfer.GetProperty("fromName").GetString().Should().Be("Admin vault");
        addTransfer.GetProperty("toName").GetString().Should().Be("Gate admin (Collateral)");
        addTransfer.GetProperty("initiatedBy").GetString().Should().Be("tester@example.com");
        Amount(addTransfer, "amount").Should().Be(0.9999m);
        await CompleteAsync(addTransfer.GetProperty("id").GetString()!);

        var removed = await PostAsync($"/admin/exchange-accounts/{accountId}/remove-collateral", new
        {
            assetId = "BTC",
            vaultAccountId = vaultId,
            amount = "0.5",
            treatAsGrossAmount = true,
        });
        var removeTransfer = (await ReadOkAsync(removed)).GetProperty("data");
        removeTransfer.GetProperty("fromName").GetString().Should().Be("Gate admin (Collateral)");
        removeTransfer.GetProperty("toName").GetString().Should().Be("Admin vault");

        var list = (await ReadOkAsync(await Http.GetAsync($"/admin/exchange-accounts/{accountId}/transfers"))).GetProperty("data");
        list.EnumerateArray().Select(item => item.GetProperty("direction").GetString()).Should().Equal("REMOVE", "ADD");

        var adminTransaction = await Fixture.AdminClient.GetTransactionAsync(addTransfer.GetProperty("id").GetString()!);
        adminTransaction.IsSuccess.Should().BeTrue();
        var raw = await ReadOkAsync(await Http.GetAsync($"/admin/transactions/{addTransfer.GetProperty("id").GetString()}"));
        raw.GetProperty("data").GetProperty("exchangeAccountId").GetString().Should().Be(accountId);
        raw.GetProperty("data").GetProperty("exchangeAccountName").GetString().Should().Be("Gate admin");
        raw.GetProperty("data").GetProperty("collateralDirection").GetString().Should().Be("ADD");
    }

    [Fact]
    public async Task Admin_Errors_Use_The_Admin_Format()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync(amount: 0.1m);

        var tooMuch = await PostAsync($"/admin/exchange-accounts/{accountId}/add-collateral", new
        {
            assetId = "BTC",
            vaultAccountId = vaultId,
            amount = "1",
        });
        tooMuch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(tooMuch)).GetProperty("error").GetProperty("code").GetString().Should().Be("INSUFFICIENT_BALANCE");

        var missing = await PostAsync($"/admin/exchange-accounts/{Guid.NewGuid()}/add-collateral", new
        {
            assetId = "BTC",
            vaultAccountId = vaultId,
            amount = "0.01",
        });
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadJsonAsync(missing)).GetProperty("error").GetProperty("code").GetString().Should().Be("EXCHANGE_ACCOUNT_NOT_FOUND");
    }

    [Fact]
    public async Task Auto_Transition_Job_Completes_Add_And_Remove_With_Correct_Balances()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await ReadOkAsync(await PostAsync("/admin/settings/auto-transitions", new { enabled = true }));

        var addId = await SubmitAddAsync(vaultId, accountId, "1");
        await WaitForStateAsync(addId, "COMPLETED", TimeSpan.FromSeconds(40));
        Amount(await GetExchangeAssetAsync(accountId), "total").Should().Be(0.9999m);
        var vaultAfterAdd = await GetVaultBalanceAsync(vaultId);
        vaultAfterAdd.Balance.Should().Be(1m);
        vaultAfterAdd.Pending.Should().Be(0m);

        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");
        await WaitForStateAsync(removal.GetProperty("id").GetString()!, "COMPLETED", TimeSpan.FromSeconds(40));

        var asset = await GetExchangeAssetAsync(accountId);
        Amount(asset, "total").Should().Be(0.5999m);
        Amount(asset, "credit").Should().Be(0.5999m);
        Amount(asset, "lockedAmount").Should().Be(0m);
        (await GetVaultBalanceAsync(vaultId)).Balance.Should().Be(1.3999m);
    }

    private async Task RunFailurePathAsync(string failurePath, string transactionId)
    {
        var admin = Fixture.AdminClient;
        switch (failurePath)
        {
            case "fail":
                (await admin.FailTransactionAsync(transactionId, "test failure")).IsSuccess.Should().BeTrue();
                break;
            case "reject":
                (await ReadOkAsync(await Http.PostAsync($"/admin/transactions/{transactionId}/reject", null)))
                    .GetProperty("data").GetProperty("state").GetString().Should().Be("REJECTED");
                break;
            case "cancel":
                (await admin.CancelTransactionAsync(transactionId)).IsSuccess.Should().BeTrue();
                break;
            case "timeout":
                (await admin.ApproveTransactionAsync(transactionId)).IsSuccess.Should().BeTrue();
                (await admin.SignTransactionAsync(transactionId)).IsSuccess.Should().BeTrue();
                (await ReadOkAsync(await Http.PostAsync($"/admin/transactions/{transactionId}/timeout", null)))
                    .GetProperty("data").GetProperty("state").GetString().Should().Be("TIMEOUT");
                break;
            case "fireblocks-cancel":
                (await Http.PostAsync($"/transactions/{transactionId}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.OK);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failurePath), failurePath, null);
        }
    }

    private async Task AssertRejectedRemovalAsync(string transactionId)
    {
        var transaction = await GetTransactionAsync(transactionId);
        transaction.GetProperty("status").GetString().Should().Be("CANCELLED");
        transaction.GetProperty("subStatus").GetString().Should().Be("3RD_PARTY_REJECTED");
    }

    private static void AssertAmounts(JsonElement transaction, decimal netAmount, decimal requestedAmount)
    {
        var amountInfo = transaction.GetProperty("amountInfo");
        Amount(amountInfo, "amount").Should().Be(netAmount);
        Amount(amountInfo, "netAmount").Should().Be(netAmount);
        Amount(amountInfo, "requestedAmount").Should().Be(requestedAmount);
        Amount(transaction, "networkFee").Should().Be(BtcFee);
        Amount(transaction, "fee").Should().Be(BtcFee);
        Amount(transaction.GetProperty("feeInfo"), "networkFee").Should().Be(BtcFee);
        transaction.GetProperty("operation").GetString().Should().Be("TRANSFER");
        transaction.GetProperty("createdAt").ValueKind.Should().Be(JsonValueKind.Number);
    }

    /// <summary>
    /// Adds deposit addresses to the BTC wallet of the vault, one save each, so that the ids follow the call order.
    /// </summary>
    private async Task AddDepositAddressesAsync(string vaultId, params (string Value, int Index)[] addresses)
    {
        using var db = Fixture.GetDbContext();
        var wallet = await db.Wallets.SingleAsync(item => item.VaultAccountId == vaultId && item.AssetId == "BTC");
        foreach (var (value, index) in addresses)
        {
            db.Addresses.Add(new Address
            {
                AddressValue = value,
                Type = "DEPOSIT",
                AddressFormat = "SEGWIT",
                Bip44AddressIndex = index,
                WalletId = wallet.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }
    }
}
