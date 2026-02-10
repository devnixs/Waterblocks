using System.Globalization;
using FluentAssertions;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class CrossAssetAddressResolutionTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Usdc_Transfer_To_Eth_Address_In_Another_Workspace_Creates_Wallet_And_Credits_Balance()
    {
        // Arrange: Workspace A with vault A1, Workspace B with vault B1
        var (workspaceAId, _) = await _fixture.CreateWorkspaceAsync("WorkspaceA");
        var (workspaceBId, _) = await _fixture.CreateWorkspaceAsync("WorkspaceB");
        var adminA = _fixture.CreateAdminClientForWorkspace(workspaceAId);
        var adminB = _fixture.CreateAdminClientForWorkspace(workspaceBId);

        var vaultAResponse = await adminA.CreateVaultAsync("A1");
        var vaultBResponse = await adminB.CreateVaultAsync("B1");
        vaultAResponse.IsSuccess.Should().BeTrue();
        vaultBResponse.IsSuccess.Should().BeTrue();

        var vaultAId = vaultAResponse.Data!.Id;
        var vaultBId = vaultBResponse.Data!.Id;

        // Create source USDC wallet and destination ETH wallet only.
        var sourceWalletResponse = await adminA.CreateWalletAsync(vaultAId, "USDC");
        var destinationEthWalletResponse = await adminB.CreateWalletAsync(vaultBId, "ETH");
        sourceWalletResponse.IsSuccess.Should().BeTrue();
        destinationEthWalletResponse.IsSuccess.Should().BeTrue();

        var vaultADetails = await adminA.GetVaultAsync(vaultAId);
        var sourceUsdcAddress = vaultADetails.Data!.Wallets.First(w => w.AssetId == "USDC").DepositAddress;
        sourceUsdcAddress.Should().NotBeNullOrWhiteSpace();

        var vaultBDetailsBefore = await adminB.GetVaultAsync(vaultBId);
        var destinationEthAddress = vaultBDetailsBefore.Data!.Wallets.First(w => w.AssetId == "ETH").DepositAddress;
        destinationEthAddress.Should().NotBeNullOrWhiteSpace();
        vaultBDetailsBefore.Data.Wallets.Should().NotContain(w => w.AssetId == "USDC");

        // Fund workspace A vault A1 with 100 USDC from external source.
        var fundResponse = await adminA.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "USDC",
            SourceAddress = "external-funder",
            DestinationAddress = sourceUsdcAddress!,
            Amount = "100",
        });
        fundResponse.IsSuccess.Should().BeTrue();

        // Act: Transfer 100 USDC to workspace B's ETH address (shared EVM address).
        var transferResponse = await adminA.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "USDC",
            SourceAddress = sourceUsdcAddress!,
            DestinationAddress = destinationEthAddress!,
            Amount = "100",
        });
        transferResponse.IsSuccess.Should().BeTrue();

        var completeResponse = await adminA.CompleteTransactionFullCycleAsync(transferResponse.Data!.Id);
        completeResponse.IsSuccess.Should().BeTrue();

        // Assert: Workspace B should now have a USDC wallet created and credited.
        var vaultBDetailsAfter = await adminB.GetVaultAsync(vaultBId);
        vaultBDetailsAfter.IsSuccess.Should().BeTrue();

        var usdcWallet = vaultBDetailsAfter.Data!.Wallets.FirstOrDefault(w => w.AssetId == "USDC");
        usdcWallet.Should().NotBeNull("USDC wallet should be auto-created on receipt");
        usdcWallet!.DepositAddress.Should().Be(destinationEthAddress, "USDC should reuse ETH address on the same blockchain");

        var creditedAmount = decimal.Parse(usdcWallet.Balance, CultureInfo.InvariantCulture);
        creditedAmount.Should().Be(100m, "Destination USDC balance should increase by transferred amount");
    }
}
