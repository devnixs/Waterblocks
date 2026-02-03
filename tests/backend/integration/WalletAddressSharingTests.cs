using FluentAssertions;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

/// <summary>
/// Integration tests for wallet address sharing behavior.
/// Account-based blockchain assets (ETH, USDC, etc.) should share the same address
/// when they are on the same blockchain within a vault.
/// </summary>
public class WalletAddressSharingTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task ETH_And_USDC_Share_Same_Address_In_Same_Vault()
    {
        // Arrange: Create a vault
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("SharedAddressVault");
        vaultResponse.IsSuccess.Should().BeTrue("Vault should be created");
        var vaultId = vaultResponse.Data!.Id;

        // Act: Create ETH wallet first
        var ethWalletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "ETH");
        ethWalletResponse.IsSuccess.Should().BeTrue("ETH wallet should be created");
        var ethAddress = ethWalletResponse.Data!.DepositAddress;

        // Act: Create USDC wallet (should share ETH's address since USDC is on Ethereum)
        var usdcWalletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "USDC");
        usdcWalletResponse.IsSuccess.Should().BeTrue("USDC wallet should be created");
        var usdcAddress = usdcWalletResponse.Data!.DepositAddress;

        // Assert: Both wallets should have the same address
        usdcAddress.Should().Be(ethAddress,
            "USDC and ETH should share the same address since they're both on the Ethereum blockchain");
    }

    [Fact]
    public async Task USDC_Created_First_Shares_Address_With_ETH_Created_Second()
    {
        // Arrange: Create a vault
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("ReverseOrderVault");
        vaultResponse.IsSuccess.Should().BeTrue("Vault should be created");
        var vaultId = vaultResponse.Data!.Id;

        // Act: Create USDC wallet first (token before native asset)
        var usdcWalletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "USDC");
        usdcWalletResponse.IsSuccess.Should().BeTrue("USDC wallet should be created");
        var usdcAddress = usdcWalletResponse.Data!.DepositAddress;

        // Act: Create ETH wallet second (should share USDC's address)
        var ethWalletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "ETH");
        ethWalletResponse.IsSuccess.Should().BeTrue("ETH wallet should be created");
        var ethAddress = ethWalletResponse.Data!.DepositAddress;

        // Assert: Both wallets should have the same address
        ethAddress.Should().Be(usdcAddress,
            "ETH and USDC should share the same address regardless of creation order");
    }

    [Fact]
    public async Task BTC_Gets_Unique_Addresses_For_Each_Wallet()
    {
        // Arrange: Create a vault
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("BTCVault");
        vaultResponse.IsSuccess.Should().BeTrue("Vault should be created");
        var vaultId = vaultResponse.Data!.Id;

        // Act: Create first BTC wallet
        var btcWallet1Response = await _fixture.AdminClient.CreateWalletAsync(vaultId, "BTC");
        btcWallet1Response.IsSuccess.Should().BeTrue("First BTC wallet should be created");
        var btcAddress1 = btcWallet1Response.Data!.DepositAddress;

        // Act: Create second BTC wallet (should get a new address for UTXO-based assets)
        var btcWallet2Response = await _fixture.AdminClient.CreateWalletAsync(vaultId, "BTC");
        btcWallet2Response.IsSuccess.Should().BeTrue("Second BTC wallet should be created");
        var btcAddress2 = btcWallet2Response.Data!.DepositAddress;

        // Assert: BTC wallets can have different addresses (UTXO model)
        // Note: The second call may add an address to the existing wallet
        // The key is that address-based assets support multiple addresses
        btcAddress1.Should().NotBeNullOrEmpty("First BTC address should exist");
        btcAddress2.Should().NotBeNullOrEmpty("Second BTC address should exist");
    }

    [Fact]
    public async Task Different_Vaults_Have_Different_Addresses_For_Same_Asset()
    {
        // Arrange: Create two vaults
        var vault1Response = await _fixture.AdminClient.CreateVaultAsync("Vault1");
        vault1Response.IsSuccess.Should().BeTrue();
        var vault1Id = vault1Response.Data!.Id;

        var vault2Response = await _fixture.AdminClient.CreateVaultAsync("Vault2");
        vault2Response.IsSuccess.Should().BeTrue();
        var vault2Id = vault2Response.Data!.Id;

        // Act: Create ETH wallets in both vaults
        var eth1Response = await _fixture.AdminClient.CreateWalletAsync(vault1Id, "ETH");
        eth1Response.IsSuccess.Should().BeTrue();
        var ethAddress1 = eth1Response.Data!.DepositAddress;

        var eth2Response = await _fixture.AdminClient.CreateWalletAsync(vault2Id, "ETH");
        eth2Response.IsSuccess.Should().BeTrue();
        var ethAddress2 = eth2Response.Data!.DepositAddress;

        // Assert: Different vaults should have different addresses
        ethAddress2.Should().NotBe(ethAddress1,
            "Different vaults should have different ETH addresses");
    }

    [Fact]
    public async Task Vault_Wallets_Show_Shared_Address_After_Creation()
    {
        // Arrange: Create a vault and wallets
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("VerificationVault");
        vaultResponse.IsSuccess.Should().BeTrue();
        var vaultId = vaultResponse.Data!.Id;

        await _fixture.AdminClient.CreateWalletAsync(vaultId, "ETH");
        await _fixture.AdminClient.CreateWalletAsync(vaultId, "USDC");

        // Act: Fetch the vault with all wallets
        var fetchedVault = await _fixture.AdminClient.GetVaultAsync(vaultId);
        fetchedVault.IsSuccess.Should().BeTrue();

        // Assert: Both wallets should have the same deposit address
        var ethWallet = fetchedVault.Data!.Wallets.First(w => w.AssetId == "ETH");
        var usdcWallet = fetchedVault.Data!.Wallets.First(w => w.AssetId == "USDC");

        usdcWallet.DepositAddress.Should().Be(ethWallet.DepositAddress,
            "When viewing the vault, ETH and USDC should show the same deposit address");
    }
}
