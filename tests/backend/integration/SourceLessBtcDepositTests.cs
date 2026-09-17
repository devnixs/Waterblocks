using FluentAssertions;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class SourceLessBtcDepositTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task SourceLess_Btc_Deposit_Matches_Fireblocks_Contract_And_Credits_Once()
    {
        var vault = await _fixture.AdminClient.CreateVaultAsync("Trading Deposit");
        vault.IsSuccess.Should().BeTrue();
        var wallet = await _fixture.AdminClient.CreateWalletAsync(vault.Data!.Id, "BTC");
        wallet.IsSuccess.Should().BeTrue();

        const string txHash = "02dfb327223ea72bcefc43ac4a1e57ac24d9aa76a12a86f368f1753b21b4dad2";
        const string blockHash = "000000000000000000022b9b810c2fac40e46e241d36df8a7a7fe5a67f0692f2";

        var created = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            DestinationAddress = wallet.Data!.DepositAddress,
            Amount = "0.01654844",
            Hash = txHash,
            NetworkFee = "0",
            IsSourceAddressUnavailable = true,
            TransactionIndex = 36,
            BlockHeight = "967336",
            BlockHash = blockHash,
        });

        created.IsSuccess.Should().BeTrue(created.Error?.Message);
        created.Data!.State.Should().Be("COMPLETED");
        created.Data.SourceAddress.Should().BeEmpty();
        created.Data.IsSourceAddressUnavailable.Should().BeTrue();
        created.Data.TransactionIndex.Should().Be(36);
        created.Data.Confirmations.Should().Be(1);

        var fireblocks = await _fixture.FireblocksClient.GetTransactionAsync(created.Data.Id);
        fireblocks.Should().NotBeNull();
        fireblocks!.Source.Should().BeEquivalentTo(new FireblocksTransferPeerPathResponse
        {
            Id = string.Empty,
            Type = "UNKNOWN",
            Name = "External",
            SubType = string.Empty,
        });
        fireblocks.SourceAddress.Should().BeEmpty();
        fireblocks.Destination!.Type.Should().Be("VAULT_ACCOUNT");
        fireblocks.Destination.Id.Should().Be(vault.Data.Id);
        fireblocks.Destination.Name.Should().Be("Trading Deposit");
        fireblocks.DestinationAddress.Should().Be(wallet.Data.DepositAddress);
        fireblocks.Status.Should().Be("COMPLETED");
        fireblocks.SubStatus.Should().Be("CONFIRMED");
        fireblocks.TxHash.Should().Be(txHash);
        fireblocks.Index.Should().Be(36);
        fireblocks.BlockInfo.BlockHeight.Should().Be("967336");
        fireblocks.BlockInfo.BlockHash.Should().Be(blockHash);
        fireblocks.NumOfConfirmations.Should().Be(1);
        fireblocks.RequestedAmount.Should().Be("0.01654844");
        fireblocks.Amount.Should().Be("0.01654844");
        fireblocks.NetAmount.Should().Be("0.01654844");
        fireblocks.AmountInfo.Amount.Should().Be("0.01654844");
        fireblocks.AmountInfo.RequestedAmount.Should().Be("0.01654844");
        fireblocks.AmountInfo.NetAmount.Should().Be("0.01654844");
        fireblocks.NetworkFee.Should().Be("0");
        fireblocks.FeeInfo.NetworkFee.Should().Be("0");
        fireblocks.Operation.Should().Be("TRANSFER");
        fireblocks.AssetType.Should().Be("BASE_ASSET");
        fireblocks.AddressType.Should().BeEmpty();

        var vaultAfterPolling = await _fixture.AdminClient.GetVaultAsync(vault.Data.Id);
        decimal.Parse(vaultAfterPolling.Data!.Wallets.Single(w => w.AssetId == "BTC").Balance)
            .Should().Be(0.01654844m);
    }

    [Fact]
    public async Task SourceLess_Deposit_Rejects_Unsupported_Asset()
    {
        var vault = await _fixture.AdminClient.CreateVaultAsync("ETH Deposit");
        var wallet = await _fixture.AdminClient.CreateWalletAsync(vault.Data!.Id, "ETH");

        var created = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            DestinationAddress = wallet.Data!.DepositAddress,
            Amount = "1",
            IsSourceAddressUnavailable = true,
        });

        created.IsSuccess.Should().BeFalse();
        created.Error!.Code.Should().Be("SOURCELESS_ASSET_UNSUPPORTED");
    }

    [Fact]
    public async Task Ordinary_External_Btc_Source_Remains_OneTimeAddress()
    {
        var vault = await _fixture.AdminClient.CreateVaultAsync("Ordinary Deposit");
        var wallet = await _fixture.AdminClient.CreateWalletAsync(vault.Data!.Id, "BTC");

        var created = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "bc1qexternalordinarysource",
            DestinationAddress = wallet.Data!.DepositAddress,
            Amount = "0.1",
        });

        created.IsSuccess.Should().BeTrue(created.Error?.Message);
        var fireblocks = await _fixture.FireblocksClient.GetTransactionAsync(created.Data!.Id);
        fireblocks!.Source!.Type.Should().Be("ONE_TIME_ADDRESS");
        fireblocks.SourceAddress.Should().Be("bc1qexternalordinarysource");
        fireblocks.Source.SubType.Should().Be("DEFAULT");
    }
}
