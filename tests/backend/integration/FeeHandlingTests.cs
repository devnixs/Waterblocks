using FluentAssertions;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

/// <summary>
/// Integration tests for transaction fee handling functionality.
/// </summary>
public class FeeHandlingTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Separate_Fee_Wallet_Is_AutoCreated_With_Deposit_Address()
    {
        // Arrange: create a custom token that pays fees in ETH.
        var assetId = "WBUSD";
        var createAssetResult = await _fixture.AdminClient.CreateAssetAsync(new CreateAdminAssetRequest
        {
            AssetId = assetId,
            Name = "Waterblocks USD",
            Symbol = "WBUSD",
            Decimals = 6,
            Type = "ERC20",
            BlockchainType = "AccountBased",
            NativeAsset = "ETH",
            BaseFee = 0.01m,
            FeeAssetId = "ETH",
            IsActive = true,
        });
        createAssetResult.IsSuccess.Should().BeTrue("test asset should be created");

        // Create a source vault with token wallet only.
        var sourceVaultResponse = await _fixture.AdminClient.CreateVaultAsync("FeeWalletSourceVault");
        sourceVaultResponse.IsSuccess.Should().BeTrue();
        var sourceVaultId = sourceVaultResponse.Data!.Id;

        var sourceTokenWalletResponse = await _fixture.AdminClient.CreateWalletAsync(sourceVaultId, assetId);
        sourceTokenWalletResponse.IsSuccess.Should().BeTrue();
        var sourceTokenAddress = sourceTokenWalletResponse.Data!.DepositAddress;

        // Fund source wallet so outgoing token transaction can reserve transfer amount.
        var fundingTx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = assetId,
            SourceAddress = "external-funder",
            DestinationAddress = sourceTokenAddress,
            Amount = "25",
        });
        fundingTx.IsSuccess.Should().BeTrue();

        // Destination address must be valid EVM format for account-based assets.
        var destinationVaultResponse = await _fixture.AdminClient.CreateVaultAsync("FeeWalletDestinationVault");
        destinationVaultResponse.IsSuccess.Should().BeTrue();
        var destinationVaultId = destinationVaultResponse.Data!.Id;
        var destinationEthWalletResponse = await _fixture.AdminClient.CreateWalletAsync(destinationVaultId, "ETH");
        destinationEthWalletResponse.IsSuccess.Should().BeTrue();
        var destinationAddress = destinationEthWalletResponse.Data!.DepositAddress;

        // Sanity: source vault should not have ETH wallet yet.
        var sourceBeforeTx = await _fixture.AdminClient.GetVaultAsync(sourceVaultId);
        sourceBeforeTx.Data!.Wallets.Should().NotContain(w => w.AssetId == "ETH");

        // Act: create outgoing token transfer (fee is paid in ETH).
        var outgoingTx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = assetId,
            SourceAddress = sourceTokenAddress,
            DestinationAddress = destinationAddress,
            Amount = "1",
            FeeLevel = "MEDIUM",
        });
        outgoingTx.IsSuccess.Should().BeFalse("source vault has no ETH fee balance yet");
        outgoingTx.Error!.Code.Should().Be("INSUFFICIENT_FEE_BALANCE");

        // Assert: auto-created ETH fee wallet has a usable deposit address.
        var sourceAfterTx = await _fixture.AdminClient.GetVaultAsync(sourceVaultId);
        var ethFeeWallet = sourceAfterTx.Data!.Wallets.FirstOrDefault(w => w.AssetId == "ETH");
        ethFeeWallet.Should().NotBeNull("token fee payment should auto-create an ETH fee wallet");
        ethFeeWallet!.AddressCount.Should().BeGreaterThan(0, "auto-created fee wallet must include at least one address");
        ethFeeWallet.DepositAddress.Should().NotBeNullOrWhiteSpace("auto-created fee wallet must expose a deposit address");
    }

    [Fact]
    public async Task Fees_Are_Deducted_From_Balance_When_Transaction_Completes()
    {
        // Arrange: Create vaults with ETH wallets
        var sourceVaultResponse = await _fixture.AdminClient.CreateVaultAsync("SourceVault");
        sourceVaultResponse.IsSuccess.Should().BeTrue("Source vault should be created");
        var sourceVaultId = sourceVaultResponse.Data!.Id;

        var sourceWalletResponse = await _fixture.AdminClient.CreateWalletAsync(sourceVaultId, "ETH");
        sourceWalletResponse.IsSuccess.Should().BeTrue("ETH wallet should be created in source vault");
        var sourceAddress = sourceWalletResponse.Data!.DepositAddress;

        var destVaultResponse = await _fixture.AdminClient.CreateVaultAsync("DestVault");
        destVaultResponse.IsSuccess.Should().BeTrue("Destination vault should be created");
        var destVaultId = destVaultResponse.Data!.Id;

        var destWalletResponse = await _fixture.AdminClient.CreateWalletAsync(destVaultId, "ETH");
        destWalletResponse.IsSuccess.Should().BeTrue("ETH wallet should be created in dest vault");
        var destAddress = destWalletResponse.Data!.DepositAddress;

        // Fund the source vault with 10 ETH
        var fundingTx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = "external-funder",
            DestinationAddress = sourceAddress,
            Amount = "10",
        });
        fundingTx.IsSuccess.Should().BeTrue("Funding transaction should succeed");

        // Verify initial balance
        var sourceVaultAfterFunding = await _fixture.AdminClient.GetVaultAsync(sourceVaultId);
        var sourceEthWallet = sourceVaultAfterFunding.Data!.Wallets.First(w => w.AssetId == "ETH");
        decimal.Parse(sourceEthWallet.Balance).Should().Be(10m, "Source vault should have 10 ETH after funding");

        // Act: Create a transaction of 1 ETH with MEDIUM fee level (default 0.002 * 1.5 = 0.003)
        var outgoingTx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = sourceAddress,
            DestinationAddress = destAddress,
            Amount = "1",
            FeeLevel = "MEDIUM",
        });
        outgoingTx.IsSuccess.Should().BeTrue("Outgoing transaction should be created");
        var txId = outgoingTx.Data!.Id;

        // Verify the transaction has the correct fee
        decimal.Parse(outgoingTx.Data.NetworkFee).Should().Be(0.003m, "Network fee should be 0.003 ETH (0.002 base * 1.5 medium multiplier)");
        outgoingTx.Data.FeeCurrency.Should().Be("ETH", "Fee currency should be ETH");

        // Complete the transaction through its full lifecycle
        var completeResult = await _fixture.AdminClient.CompleteTransactionFullCycleAsync(txId);
        completeResult.IsSuccess.Should().BeTrue("Transaction should complete successfully");
        completeResult.Data!.State.Should().Be("COMPLETED");

        // Assert: Verify balances after completion - fee should be deducted
        var sourceVaultFinal = await _fixture.AdminClient.GetVaultAsync(sourceVaultId);
        var sourceEthWalletFinal = sourceVaultFinal.Data!.Wallets.First(w => w.AssetId == "ETH");

        // Source should have: 10 - 1 (amount) - 0.003 (fee) = 8.997 ETH
        decimal.Parse(sourceEthWalletFinal.Balance).Should().Be(8.997m,
            "Source vault should have 8.997 ETH (10 - 1 amount - 0.003 fee)");
        decimal.Parse(sourceEthWalletFinal.Pending).Should().Be(0m,
            "Pending should be 0 after transaction completes");

        var destVaultFinal = await _fixture.AdminClient.GetVaultAsync(destVaultId);
        var destEthWalletFinal = destVaultFinal.Data!.Wallets.First(w => w.AssetId == "ETH");
        decimal.Parse(destEthWalletFinal.Balance).Should().Be(1m,
            "Destination vault should have 1 ETH (the transfer amount, not including fee)");
    }

    [Fact]
    public async Task Fees_Are_Reserved_In_Pending_When_Transaction_Is_Created()
    {
        // Arrange: Create vault with ETH wallet
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("FeeTestVault");
        vaultResponse.IsSuccess.Should().BeTrue();
        var vaultId = vaultResponse.Data!.Id;

        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "ETH");
        walletResponse.IsSuccess.Should().BeTrue();
        var address = walletResponse.Data!.DepositAddress;

        // Fund with 5 ETH
        await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = "funder",
            DestinationAddress = address,
            Amount = "5",
        });

        // Act: Create outgoing transaction
        var tx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = address,
            DestinationAddress = "external-dest",
            Amount = "1",
            FeeLevel = "MEDIUM",
        });
        tx.IsSuccess.Should().BeTrue();

        // Assert: Pending should include both amount and fee
        var vaultAfterTx = await _fixture.AdminClient.GetVaultAsync(vaultId);
        var wallet = vaultAfterTx.Data!.Wallets.First(w => w.AssetId == "ETH");

        // Pending should be 1 (amount) + 0.003 (fee) = 1.003
        decimal.Parse(wallet.Pending).Should().Be(1.003m,
            "Pending should include both amount (1) and fee (0.003)");
        decimal.Parse(wallet.Balance).Should().Be(5m,
            "Balance should remain unchanged until completion");
    }

    [Fact]
    public async Task Fees_Are_Rolled_Back_When_Transaction_Fails()
    {
        // Arrange: Create vault with ETH wallet
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("FeeRollbackVault");
        vaultResponse.IsSuccess.Should().BeTrue();
        var vaultId = vaultResponse.Data!.Id;

        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "ETH");
        walletResponse.IsSuccess.Should().BeTrue();
        var address = walletResponse.Data!.DepositAddress;

        // Fund with 3 ETH
        await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = "funder",
            DestinationAddress = address,
            Amount = "3",
        });

        // Create outgoing transaction
        var tx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = address,
            DestinationAddress = "external-dest",
            Amount = "1",
            FeeLevel = "HIGH", // 0.002 * 2.5 = 0.005
        });
        tx.IsSuccess.Should().BeTrue();

        // Verify pending is set (1 + 0.005 = 1.005)
        var vaultDuringPending = await _fixture.AdminClient.GetVaultAsync(vaultId);
        var walletDuringPending = vaultDuringPending.Data!.Wallets.First(w => w.AssetId == "ETH");
        decimal.Parse(walletDuringPending.Pending).Should().Be(1.005m);

        // Act: Fail the transaction
        var failResult = await _fixture.AdminClient.FailTransactionAsync(tx.Data!.Id, "NETWORK_ERROR");
        failResult.IsSuccess.Should().BeTrue();

        // Assert: Both amount and fee should be rolled back
        var vaultAfterFail = await _fixture.AdminClient.GetVaultAsync(vaultId);
        var walletAfterFail = vaultAfterFail.Data!.Wallets.First(w => w.AssetId == "ETH");
        decimal.Parse(walletAfterFail.Balance).Should().Be(3m,
            "Balance should remain at 3 ETH after failed transaction");
        decimal.Parse(walletAfterFail.Pending).Should().Be(0m,
            "Pending should be rolled back to 0 after failed transaction (including fee)");
    }

    [Fact]
    public async Task TreatAsGrossAmount_Deducts_Fee_From_Transfer_Amount()
    {
        // Arrange: Create vaults with ETH wallets
        var sourceVaultResponse = await _fixture.AdminClient.CreateVaultAsync("GrossSourceVault");
        sourceVaultResponse.IsSuccess.Should().BeTrue();
        var sourceVaultId = sourceVaultResponse.Data!.Id;

        var sourceWalletResponse = await _fixture.AdminClient.CreateWalletAsync(sourceVaultId, "ETH");
        sourceWalletResponse.IsSuccess.Should().BeTrue();
        var sourceAddress = sourceWalletResponse.Data!.DepositAddress;

        var destVaultResponse = await _fixture.AdminClient.CreateVaultAsync("GrossDestVault");
        destVaultResponse.IsSuccess.Should().BeTrue();
        var destVaultId = destVaultResponse.Data!.Id;

        var destWalletResponse = await _fixture.AdminClient.CreateWalletAsync(destVaultId, "ETH");
        destWalletResponse.IsSuccess.Should().BeTrue();
        var destAddress = destWalletResponse.Data!.DepositAddress;

        // Fund with exactly 1 ETH
        await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = "funder",
            DestinationAddress = sourceAddress,
            Amount = "1",
        });

        // Act: Create transaction with TreatAsGrossAmount=true
        // User wants to send "1 ETH total" including fee
        var tx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = sourceAddress,
            DestinationAddress = destAddress,
            Amount = "1",
            FeeLevel = "MEDIUM", // 0.003 ETH
            TreatAsGrossAmount = true,
        });
        tx.IsSuccess.Should().BeTrue("Transaction should be created with gross amount");
        tx.Data!.TreatAsGrossAmount.Should().BeTrue();

        // The actual transfer amount should be 1 - 0.003 = 0.997
        decimal.Parse(tx.Data.Amount).Should().Be(0.997m,
            "Transfer amount should be 0.997 (1 - 0.003 fee)");
        decimal.Parse(tx.Data.NetworkFee).Should().Be(0.003m,
            "Network fee should still be 0.003 ETH");

        // Complete the transaction
        var completeResult = await _fixture.AdminClient.CompleteTransactionFullCycleAsync(tx.Data.Id);
        completeResult.IsSuccess.Should().BeTrue();

        // Assert: Source should be completely drained (sent exactly 1 ETH total)
        var sourceVaultFinal = await _fixture.AdminClient.GetVaultAsync(sourceVaultId);
        var sourceWalletFinal = sourceVaultFinal.Data!.Wallets.First(w => w.AssetId == "ETH");
        decimal.Parse(sourceWalletFinal.Balance).Should().Be(0m,
            "Source vault should be empty (sent 0.997 amount + 0.003 fee = 1 ETH total)");

        // Destination should receive only the transfer amount (not the fee)
        var destVaultFinal = await _fixture.AdminClient.GetVaultAsync(destVaultId);
        var destWalletFinal = destVaultFinal.Data!.Wallets.First(w => w.AssetId == "ETH");
        decimal.Parse(destWalletFinal.Balance).Should().Be(0.997m,
            "Destination vault should have 0.997 ETH (transfer amount after fee deduction)");
    }

    [Fact]
    public async Task Different_Fee_Levels_Result_In_Different_Fees()
    {
        // Arrange: Create vault with ETH wallet
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("FeeLevelTestVault");
        vaultResponse.IsSuccess.Should().BeTrue();
        var vaultId = vaultResponse.Data!.Id;

        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "ETH");
        walletResponse.IsSuccess.Should().BeTrue();
        var address = walletResponse.Data!.DepositAddress;

        // Fund with enough ETH
        await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = "funder",
            DestinationAddress = address,
            Amount = "100",
        });

        // Act & Assert: Create transactions with different fee levels
        // ETH base fee is 0.002

        // LOW fee: 0.002 * 1.0 = 0.002
        var lowFeeTx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = address,
            DestinationAddress = "dest1",
            Amount = "1",
            FeeLevel = "LOW",
        });
        lowFeeTx.IsSuccess.Should().BeTrue();
        decimal.Parse(lowFeeTx.Data!.NetworkFee).Should().Be(0.002m, "LOW fee should be 0.002 (base * 1.0)");

        // MEDIUM fee: 0.002 * 1.5 = 0.003
        var mediumFeeTx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = address,
            DestinationAddress = "dest2",
            Amount = "1",
            FeeLevel = "MEDIUM",
        });
        mediumFeeTx.IsSuccess.Should().BeTrue();
        decimal.Parse(mediumFeeTx.Data!.NetworkFee).Should().Be(0.003m, "MEDIUM fee should be 0.003 (base * 1.5)");

        // HIGH fee: 0.002 * 2.5 = 0.005
        var highFeeTx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = address,
            DestinationAddress = "dest3",
            Amount = "1",
            FeeLevel = "HIGH",
        });
        highFeeTx.IsSuccess.Should().BeTrue();
        decimal.Parse(highFeeTx.Data!.NetworkFee).Should().Be(0.005m, "HIGH fee should be 0.005 (base * 2.5)");
    }

    [Fact]
    public async Task Fireblocks_API_NetAmount_Equals_Amount_When_TreatAsGrossAmount_Is_False()
    {
        // Arrange: Create vault with ETH wallet
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("NetAmountTestVault");
        vaultResponse.IsSuccess.Should().BeTrue();
        var vaultId = vaultResponse.Data!.Id;

        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "ETH");
        walletResponse.IsSuccess.Should().BeTrue();
        var address = walletResponse.Data!.DepositAddress;

        // Fund with 10 ETH
        await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = "funder",
            DestinationAddress = address,
            Amount = "10",
        });

        // Act: Create transaction with TreatAsGrossAmount=false (default)
        // This means fees are paid separately - recipient should receive the full amount
        var tx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = address,
            DestinationAddress = "external-dest",
            Amount = "1",
            FeeLevel = "MEDIUM", // 0.003 ETH fee
            TreatAsGrossAmount = false,
        });
        tx.IsSuccess.Should().BeTrue();
        tx.Data!.TreatAsGrossAmount.Should().BeFalse();

        // Complete the transaction
        await _fixture.AdminClient.CompleteTransactionFullCycleAsync(tx.Data.Id);

        // Assert: Fetch via Fireblocks API and verify netAmount equals amount
        var fireblocksTransaction = await _fixture.FireblocksClient.GetTransactionAsync(tx.Data.Id);
        fireblocksTransaction.Should().NotBeNull();

        decimal.Parse(fireblocksTransaction!.Amount).Should().Be(1m, "Amount should be 1 ETH");
        decimal.Parse(fireblocksTransaction.NetworkFee).Should().Be(0.003m, "NetworkFee should be 0.003 ETH");
        decimal.Parse(fireblocksTransaction.NetAmount).Should().Be(1m,
            "NetAmount should equal Amount (1 ETH) when TreatAsGrossAmount is false - fees are paid separately");
    }

    [Fact]
    public async Task Fireblocks_API_NetAmount_Equals_Amount_When_TreatAsGrossAmount_Is_True()
    {
        // Arrange: Create vault with ETH wallet
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("NetAmountGrossTestVault");
        vaultResponse.IsSuccess.Should().BeTrue();
        var vaultId = vaultResponse.Data!.Id;

        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "ETH");
        walletResponse.IsSuccess.Should().BeTrue();
        var address = walletResponse.Data!.DepositAddress;

        // Fund with 10 ETH
        await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = "funder",
            DestinationAddress = address,
            Amount = "10",
        });

        // Act: Create transaction with TreatAsGrossAmount=true
        // This means fees are deducted from the requested amount before transfer
        // User requests 1 ETH total, so Amount stored = 1 - 0.003 = 0.997
        var tx = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = address,
            DestinationAddress = "external-dest",
            Amount = "1",
            FeeLevel = "MEDIUM", // 0.003 ETH fee
            TreatAsGrossAmount = true,
        });
        tx.IsSuccess.Should().BeTrue();
        tx.Data!.TreatAsGrossAmount.Should().BeTrue();

        // The actual transfer amount should be 1 - 0.003 = 0.997
        decimal.Parse(tx.Data.Amount).Should().Be(0.997m, "Transfer amount should be reduced by fee");

        // Complete the transaction
        await _fixture.AdminClient.CompleteTransactionFullCycleAsync(tx.Data.Id);

        // Assert: Fetch via Fireblocks API and verify netAmount equals amount
        // NetworkFee is paid to the network, NOT deducted from what recipient receives
        var fireblocksTransaction = await _fixture.FireblocksClient.GetTransactionAsync(tx.Data.Id);
        fireblocksTransaction.Should().NotBeNull();

        decimal.Parse(fireblocksTransaction!.Amount).Should().Be(0.997m,
            "Amount should be 0.997 ETH (1 - 0.003 fee)");
        decimal.Parse(fireblocksTransaction.NetworkFee).Should().Be(0.003m,
            "NetworkFee should be 0.003 ETH");
        decimal.Parse(fireblocksTransaction.NetAmount).Should().Be(0.997m,
            "NetAmount should equal Amount (0.997 ETH) - recipient receives the full transfer amount");
    }
}
