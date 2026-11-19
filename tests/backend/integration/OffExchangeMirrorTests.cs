using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Models;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class OffExchangeMirrorTests : OffExchangeTestBase
{
    [Fact]
    public async Task Add_Sends_One_Event_After_Completed()
    {
        var accountId = await CreateConnectedAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        var transactionId = await SubmitAddAsync(vaultId, accountId, "1");

        await Task.Delay(1500);
        Simulator.EventRequests.Should().Be(0, "an add has no mirror event before COMPLETED");

        await CompleteAsync(transactionId);
        await WaitForMirrorStateAsync(accountId, transactionId, "APPLIED");

        var mirrored = Simulator.FindEvent(transactionId);
        mirrored.Should().NotBeNull();
        mirrored!.Type.Should().Be("add");
        mirrored.Amount.Should().Be(0.9999m);
        mirrored.Account.Should().Be(SimulatorAccount);
        Simulator.GetSpotAvailable(SimulatorAccount, "BTC").Should().Be(0.9999m);

        await Task.Delay(1500);
        Simulator.EventRequests.Should().Be(1, "an applied event is not sent again");
    }

    [Fact]
    public async Task Network_Failure_Retries_Without_A_Second_Credit()
    {
        var accountId = await CreateConnectedAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        Simulator.LoseNextEventReply = true;

        var transactionId = await AddAndCompleteAsync(vaultId, accountId, "1");

        await WaitForMirrorStateAsync(accountId, transactionId, "APPLIED");
        Simulator.EventRequests.Should().BeGreaterThan(1, "the lost reply causes a second request");
        Simulator.GetSpotAvailable(SimulatorAccount, "BTC").Should().Be(0.9999m);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Server_Error_Keeps_The_Event_Pending(HttpStatusCode status)
    {
        var accountId = await CreateConnectedAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        Simulator.ForcedEventStatus = status;

        var transactionId = await AddAndCompleteAsync(vaultId, accountId, "1");
        await WaitUntilAsync(() => Task.FromResult(Simulator.EventRequests >= 2), "the worker sends the event again");
        (await GetTransferAsync(accountId, transactionId)).GetProperty("mirrorState").GetString().Should().Be("PENDING");

        Simulator.ForcedEventStatus = null;
        await WaitForMirrorStateAsync(accountId, transactionId, "APPLIED");
        Simulator.GetSpotAvailable(SimulatorAccount, "BTC").Should().Be(0.9999m);
    }

    [Fact]
    public async Task Removal_Waits_For_Approval_Before_It_Can_Move()
    {
        var (accountId, vaultId) = await CreateConnectedAccountWithCollateralAsync();
        Simulator.IsOffline = true;

        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");
        var transactionId = removal.GetProperty("id").GetString()!;
        removal.GetProperty("status").GetString().Should().Be("SUBMITTED");

        var blocked = await Fixture.AdminClient.ApproveTransactionAsync(transactionId);
        blocked.IsSuccess.Should().BeFalse();
        blocked.Error!.Code.Should().Be("EXCHANGE_APPROVAL_PENDING");
        (await GetTransactionAsync(transactionId)).GetProperty("status").GetString().Should().Be("SUBMITTED");

        Simulator.IsOffline = false;
        await WaitForMirrorStateAsync(accountId, transactionId, "APPLIED");
        Simulator.FindEvent(transactionId)!.Amount.Should().Be(0.4m);
        Simulator.GetSpotAvailable(SimulatorAccount, "BTC").Should().Be(0.5999m);

        await CompleteAsync(transactionId);
        Amount(await ReadTotalsAsync(accountId), "total").Should().Be(0.5999m);
        (await GetVaultBalanceAsync(vaultId)).Balance.Should().Be(1.3999m);
    }

    [Fact]
    public async Task Auto_Transition_Does_Not_Move_A_Removal_Before_Approval()
    {
        var (accountId, vaultId) = await CreateConnectedAccountWithCollateralAsync();
        Simulator.IsOffline = true;
        await ReadOkAsync(await PostAsync("/admin/settings/auto-transitions", new { enabled = true }));

        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");
        var transactionId = removal.GetProperty("id").GetString()!;

        await Task.Delay(TimeSpan.FromSeconds(7));
        (await GetTransactionAsync(transactionId)).GetProperty("status").GetString().Should().Be("SUBMITTED");

        Simulator.IsOffline = false;
        await WaitForStateAsync(transactionId, "COMPLETED", TimeSpan.FromSeconds(40));
        Amount(await ReadTotalsAsync(accountId), "total").Should().Be(0.5999m);
    }

    [Fact]
    public async Task Rejected_Event_Cancels_The_Removal_And_Releases_The_Lock()
    {
        var (accountId, vaultId) = await CreateConnectedAccountWithCollateralAsync();
        Simulator.SetBalance(SimulatorAccount, "BTC", spotAvailable: 0.1m);

        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");
        var transactionId = removal.GetProperty("id").GetString()!;

        await WaitForStateAsync(transactionId, "CANCELLED");
        var transaction = await GetTransactionAsync(transactionId);
        transaction.GetProperty("subStatus").GetString().Should().Be("3RD_PARTY_REJECTED");
        var transfer = await GetTransferAsync(accountId, transactionId);
        transfer.GetProperty("mirrorState").GetString().Should().Be("REJECTED");
        transfer.GetProperty("mirrorError").GetString().Should().Contain("available");
        Amount(await ReadTotalsAsync(accountId), "lockedAmount").Should().Be(0m);
        Simulator.GetSpotAvailable(SimulatorAccount, "BTC").Should().Be(0.1m);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task Http_4xx_Fails_The_Removal_And_Keeps_The_Error(HttpStatusCode status)
    {
        var (accountId, vaultId) = await CreateConnectedAccountWithCollateralAsync();
        Simulator.ForcedEventStatus = status;

        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");
        var transactionId = removal.GetProperty("id").GetString()!;

        await WaitForStateAsync(transactionId, "FAILED");
        var transaction = await GetTransactionAsync(transactionId);
        transaction.GetProperty("subStatus").GetString().Should().Be("3RD_PARTY_FAILED");
        var transfer = await GetTransferAsync(accountId, transactionId);
        transfer.GetProperty("mirrorState").GetString().Should().Be("FAILED");
        transfer.GetProperty("mirrorError").GetString().Should().Contain("refused");
        Amount(await ReadTotalsAsync(accountId), "lockedAmount").Should().Be(0m);
    }

    [Fact]
    public async Task Approved_Removal_That_Fails_Creates_A_Reversal()
    {
        var (accountId, vaultId) = await CreateConnectedAccountWithCollateralAsync();
        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");
        var transactionId = removal.GetProperty("id").GetString()!;
        await WaitForMirrorStateAsync(accountId, transactionId, "APPLIED");
        Simulator.GetSpotAvailable(SimulatorAccount, "BTC").Should().Be(0.5999m);

        (await Fixture.AdminClient.FailTransactionAsync(transactionId, "after approval")).IsSuccess.Should().BeTrue();

        var reversalId = transactionId + ":reversal";
        await WaitUntilAsync(() => Task.FromResult(Simulator.FindEvent(reversalId) != null), "the reversal reaches the simulator");
        var reversal = Simulator.FindEvent(reversalId)!;
        reversal.Type.Should().Be("add");
        reversal.Amount.Should().Be(0.4m);
        Simulator.GetSpotAvailable(SimulatorAccount, "BTC").Should().Be(0.9999m);
        Amount(await ReadTotalsAsync(accountId), "lockedAmount").Should().Be(0m);
    }

    [Fact]
    public async Task Failure_Step_Cancels_A_Pending_Event_And_Nothing_Is_Sent()
    {
        var (accountId, vaultId) = await CreateConnectedAccountWithCollateralAsync();
        Simulator.IsOffline = true;
        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");
        var transactionId = removal.GetProperty("id").GetString()!;

        (await Fixture.AdminClient.CancelTransactionAsync(transactionId)).IsSuccess.Should().BeTrue();

        (await GetTransferAsync(accountId, transactionId)).GetProperty("mirrorState").GetString().Should().Be("CANCELLED");
        var requestsWhileOffline = Simulator.EventRequests;
        Simulator.IsOffline = false;
        await Task.Delay(2500);
        Simulator.EventRequests.Should().Be(requestsWhileOffline);
        Simulator.FindEvent(transactionId).Should().BeNull();
        Amount(await ReadTotalsAsync(accountId), "lockedAmount").Should().Be(0m);
    }

    [Fact]
    public async Task Event_Applied_After_The_Removal_Failed_Is_Reversed()
    {
        var (accountId, vaultId) = await CreateConnectedAccountWithCollateralAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Simulator.HoldEventReplies = release.Task;

        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");
        var transactionId = removal.GetProperty("id").GetString()!;
        await WaitUntilAsync(() => Task.FromResult(Simulator.FindEvent(transactionId) != null), "the simulator applies the event");

        (await Fixture.AdminClient.FailTransactionAsync(transactionId, "during the call")).IsSuccess.Should().BeTrue();
        Simulator.HoldEventReplies = null;
        release.SetResult();

        var reversalId = transactionId + ":reversal";
        await WaitUntilAsync(() => Task.FromResult(Simulator.FindEvent(reversalId) != null), "the reversal reaches the simulator");
        Simulator.GetSpotAvailable(SimulatorAccount, "BTC").Should().Be(0.9999m);
        (await GetTransactionAsync(transactionId)).GetProperty("status").GetString().Should().Be("FAILED");
        Amount(await ReadTotalsAsync(accountId), "lockedAmount").Should().Be(0m);
    }

    [Fact]
    public async Task Account_Without_A_Connection_Creates_No_Events_And_Does_Not_Wait()
    {
        var accountId = await CreateExchangeAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        await AddAndCompleteAsync(vaultId, accountId, "1");
        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");

        await CompleteAsync(removal.GetProperty("id").GetString()!);

        var database = Fixture.GetDbContext();
        (await database.ExchangeMirrorEvents.CountAsync()).Should().Be(0);
        Simulator.EventRequests.Should().Be(0);
    }

    [Fact]
    public async Task Events_Of_One_Account_Keep_Their_Order_After_A_Failure()
    {
        var accountId = await CreateConnectedAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        Simulator.IsOffline = true;
        var firstAdd = await AddAndCompleteAsync(vaultId, accountId, "0.5");
        var removal = await SubmitRemoveAsync(vaultId, accountId, "0.4");
        var removalId = removal.GetProperty("id").GetString()!;

        Simulator.IsOffline = false;

        await WaitForMirrorStateAsync(accountId, firstAdd, "APPLIED");
        await WaitForMirrorStateAsync(accountId, removalId, "APPLIED");
        (await GetTransactionAsync(removalId)).GetProperty("status").GetString().Should().Be("SUBMITTED");
        Simulator.GetSpotAvailable(SimulatorAccount, "BTC").Should().Be(0.0999m);
        var database = Fixture.GetDbContext();
        var statuses = await database.ExchangeMirrorEvents.AsNoTracking().Select(item => item.Status).ToListAsync();
        statuses.Should().OnlyContain(status => status == ExchangeMirrorEventStatus.APPLIED);
    }

    private async Task<(string AccountId, string VaultId)> CreateConnectedAccountWithCollateralAsync()
    {
        var accountId = await CreateConnectedAccountAsync();
        var vaultId = await CreateFundedVaultAsync();
        var addId = await AddAndCompleteAsync(vaultId, accountId, "1");
        await WaitForMirrorStateAsync(accountId, addId, "APPLIED");
        return (accountId, vaultId);
    }

    private Task<System.Text.Json.JsonElement> ReadTotalsAsync(string accountId)
    {
        return GetExchangeAssetAsync(accountId);
    }
}
