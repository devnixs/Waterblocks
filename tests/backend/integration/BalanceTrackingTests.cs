using FluentAssertions;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

/// <summary>
/// Integration tests for vault balance tracking functionality.
/// </summary>
public class BalanceTrackingTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Balance_Is_Tracked_Correctly_Across_Transactions()
    {
        // Arrange: Create vaults with BTC wallets
        var depositVaultResponse = await _fixture.AdminClient.CreateVaultAsync("Deposit");
        depositVaultResponse.IsSuccess.Should().BeTrue("Deposit vault should be created");
        var depositVaultId = depositVaultResponse.Data!.Id;

        var depositWalletResponse = await _fixture.AdminClient.CreateWalletAsync(depositVaultId, "BTC");
        depositWalletResponse.IsSuccess.Should().BeTrue("BTC wallet should be created in Deposit vault");

        var withdrawalVaultResponse = await _fixture.AdminClient.CreateVaultAsync("Withdrawal");
        withdrawalVaultResponse.IsSuccess.Should().BeTrue("Withdrawal vault should be created");
        var withdrawalVaultId = withdrawalVaultResponse.Data!.Id;

        var withdrawalWalletResponse = await _fixture.AdminClient.CreateWalletAsync(withdrawalVaultId, "BTC");
        withdrawalWalletResponse.IsSuccess.Should().BeTrue("BTC wallet should be created in Withdrawal vault");

        // Act 1: Try to create a transaction of 1 BTC from Deposit to Withdrawal (should fail - no balance)
        var failedTxResponse = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "INTERNAL",
            SourceVaultAccountId = depositVaultId,
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = withdrawalVaultId,
            Amount = "1"
        });

        // Assert 1: Transaction should be rejected due to insufficient balance
        failedTxResponse.IsSuccess.Should().BeFalse("Transaction should fail due to insufficient balance");
        failedTxResponse.Error.Should().NotBeNull();
        failedTxResponse.Error!.Code.Should().Be("INSUFFICIENT_BALANCE");

        // Act 2: Create a transaction of 1 BTC from external address to Deposit
        var incomingTxResponse = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "EXTERNAL",
            SourceAddress = "0x12345",
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = depositVaultId,
            Amount = "1"
        });

        // Assert 2: Incoming transaction should be created and completed automatically
        incomingTxResponse.IsSuccess.Should().BeTrue("Incoming transaction should be created");
        incomingTxResponse.Data!.State.Should().Be("COMPLETED", "Incoming transactions should auto-complete");

        // Verify Deposit vault now has 1 BTC
        var depositVaultAfterIncoming = await _fixture.AdminClient.GetVaultAsync(depositVaultId);
        depositVaultAfterIncoming.IsSuccess.Should().BeTrue();
        var depositBtcWallet = depositVaultAfterIncoming.Data!.Wallets.FirstOrDefault(w => w.AssetId == "BTC");
        depositBtcWallet.Should().NotBeNull();
        decimal.Parse(depositBtcWallet!.Balance).Should().Be(1m, "Deposit vault should have 1 BTC after incoming transaction");

        // Act 3: Create a transaction of 0.1 BTC from Deposit to Withdrawal
        var outgoingTxResponse = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "INTERNAL",
            SourceVaultAccountId = depositVaultId,
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = withdrawalVaultId,
            Amount = "0.1"
        });

        // Assert 3: Outgoing transaction should be created successfully
        outgoingTxResponse.IsSuccess.Should().BeTrue("Outgoing transaction should be created when sufficient balance exists");
        var outgoingTxId = outgoingTxResponse.Data!.Id;
        outgoingTxResponse.Data.State.Should().Be("SUBMITTED");

        // Complete the transaction through its full lifecycle
        var completeResult = await _fixture.AdminClient.CompleteTransactionFullCycleAsync(outgoingTxId);
        completeResult.IsSuccess.Should().BeTrue("Transaction should complete successfully");
        completeResult.Data!.State.Should().Be("COMPLETED");

        // Assert: Verify final balances
        var depositVaultFinal = await _fixture.AdminClient.GetVaultAsync(depositVaultId);
        depositVaultFinal.IsSuccess.Should().BeTrue();
        var depositBtcWalletFinal = depositVaultFinal.Data!.Wallets.FirstOrDefault(w => w.AssetId == "BTC");
        depositBtcWalletFinal.Should().NotBeNull();
        decimal.Parse(depositBtcWalletFinal!.Balance).Should().Be(0.9m, "Deposit vault should have 0.9 BTC after outgoing transaction");

        var withdrawalVaultFinal = await _fixture.AdminClient.GetVaultAsync(withdrawalVaultId);
        withdrawalVaultFinal.IsSuccess.Should().BeTrue();
        var withdrawalBtcWalletFinal = withdrawalVaultFinal.Data!.Wallets.FirstOrDefault(w => w.AssetId == "BTC");
        withdrawalBtcWalletFinal.Should().NotBeNull();
        decimal.Parse(withdrawalBtcWalletFinal!.Balance).Should().Be(0.1m, "Withdrawal vault should have 0.1 BTC after receiving transaction");
    }

    [Fact]
    public async Task Pending_Balance_Is_Rolled_Back_On_Transaction_Failure()
    {
        // Arrange: Create vault with BTC wallet and fund it
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("TestVault");
        vaultResponse.IsSuccess.Should().BeTrue();
        var vaultId = vaultResponse.Data!.Id;

        await _fixture.AdminClient.CreateWalletAsync(vaultId, "BTC");

        // Fund the vault with 1 BTC
        var fundingTx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "EXTERNAL",
            SourceAddress = "external-address",
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = vaultId,
            Amount = "1"
        });
        fundingTx.IsSuccess.Should().BeTrue();

        // Act: Create an outgoing transaction and then fail it
        var outgoingTx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "INTERNAL",
            SourceVaultAccountId = vaultId,
            DestinationType = "EXTERNAL",
            DestinationAddress = "external-destination",
            Amount = "0.5"
        });
        outgoingTx.IsSuccess.Should().BeTrue();

        // Verify pending is set
        var vaultDuringPending = await _fixture.AdminClient.GetVaultAsync(vaultId);
        var walletDuringPending = vaultDuringPending.Data!.Wallets.First(w => w.AssetId == "BTC");
        decimal.Parse(walletDuringPending.Pending).Should().Be(0.5m, "Pending should reflect reserved amount");

        // Fail the transaction
        var failResult = await _fixture.AdminClient.FailTransactionAsync(outgoingTx.Data!.Id, "NETWORK_ERROR");
        failResult.IsSuccess.Should().BeTrue();

        // Assert: Verify pending is rolled back and balance is unchanged
        var vaultAfterFail = await _fixture.AdminClient.GetVaultAsync(vaultId);
        var walletAfterFail = vaultAfterFail.Data!.Wallets.First(w => w.AssetId == "BTC");
        decimal.Parse(walletAfterFail.Balance).Should().Be(1m, "Balance should remain at 1 BTC after failed transaction");
        decimal.Parse(walletAfterFail.Pending).Should().Be(0m, "Pending should be rolled back to 0 after failed transaction");
    }

    [Fact]
    public async Task Cannot_Create_Transaction_When_Balance_Is_Locked_By_Pending()
    {
        // Arrange: Create vault with BTC wallet and fund it with exactly 1 BTC
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("LimitedVault");
        vaultResponse.IsSuccess.Should().BeTrue();
        var vaultId = vaultResponse.Data!.Id;

        await _fixture.AdminClient.CreateWalletAsync(vaultId, "BTC");

        // Fund with 1 BTC
        await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "EXTERNAL",
            SourceAddress = "funder",
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = vaultId,
            Amount = "1"
        });

        // Create first transaction for 0.6 BTC (reserves it as pending)
        var firstTx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "INTERNAL",
            SourceVaultAccountId = vaultId,
            DestinationType = "EXTERNAL",
            DestinationAddress = "dest1",
            Amount = "0.6"
        });
        firstTx.IsSuccess.Should().BeTrue();

        // Act: Try to create second transaction for 0.6 BTC (should fail - only 0.4 available)
        var secondTx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "INTERNAL",
            SourceVaultAccountId = vaultId,
            DestinationType = "EXTERNAL",
            DestinationAddress = "dest2",
            Amount = "0.6"
        });

        // Assert: Second transaction should fail due to insufficient available balance
        secondTx.IsSuccess.Should().BeFalse();
        secondTx.Error!.Code.Should().Be("INSUFFICIENT_BALANCE");
    }
}
