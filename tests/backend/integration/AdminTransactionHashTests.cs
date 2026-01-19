using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class AdminTransactionHashTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AdminTransactionHashTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CanCreateTransactionWithCustomHash()
    {
        // Create vault
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("Test Vault");
        Assert.NotNull(vaultResponse.Data);
        var vaultId = vaultResponse.Data!.Id;

        // Create BTC wallet and get deposit address
        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "BTC");
        Assert.NotNull(walletResponse.Data);
        var depositAddress = walletResponse.Data!.DepositAddress;

        // Create transaction with custom hash
        var customHash = "a1b2c3d4e5f6789012345678901234567890123456789012345678901234abcd";
        var createResponse = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-btc-address",
            DestinationAddress = depositAddress,
            Amount = "1.5",
            Hash = customHash
        });

        Assert.NotNull(createResponse.Data);
        Assert.Equal(customHash, createResponse.Data!.Hash);
    }

    [Fact]
    public async Task CannotCreateTransactionWithDuplicateHash()
    {
        // Create vault
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("Test Vault");
        Assert.NotNull(vaultResponse.Data);
        var vaultId = vaultResponse.Data!.Id;

        // Create BTC wallet and get deposit address
        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "BTC");
        Assert.NotNull(walletResponse.Data);
        var depositAddress = walletResponse.Data!.DepositAddress;

        // Create first transaction with custom hash
        var customHash = "b2c3d4e5f6789012345678901234567890123456789012345678901234abcdef";
        var createResponse1 = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-btc-address-1",
            DestinationAddress = depositAddress,
            Amount = "1.0",
            Hash = customHash
        });

        Assert.NotNull(createResponse1.Data);
        Assert.Equal(customHash, createResponse1.Data!.Hash);

        // Try to create second transaction with same hash - should fail
        var createResponse2 = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-btc-address-2",
            DestinationAddress = depositAddress,
            Amount = "2.0",
            Hash = customHash
        });

        Assert.Null(createResponse2.Data);
        Assert.NotNull(createResponse2.Error);
        Assert.Equal("DUPLICATE_HASH", createResponse2.Error!.Code);
        Assert.Contains("already exists", createResponse2.Error.Message);
    }

    [Fact]
    public async Task GeneratesHashAutomaticallyWhenNotProvided()
    {
        // Create vault
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("Test Vault");
        Assert.NotNull(vaultResponse.Data);
        var vaultId = vaultResponse.Data!.Id;

        // Create BTC wallet and get deposit address
        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "BTC");
        Assert.NotNull(walletResponse.Data);
        var depositAddress = walletResponse.Data!.DepositAddress;

        // Create transaction without providing hash
        var createResponse = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-btc-address",
            DestinationAddress = depositAddress,
            Amount = "1.5"
        });

        Assert.NotNull(createResponse.Data);
        Assert.NotNull(createResponse.Data!.Hash);
        Assert.NotEmpty(createResponse.Data.Hash);
        // BTC should generate hash without 0x prefix
        Assert.DoesNotContain("0x", createResponse.Data.Hash);
        // Should be 64 hex characters
        Assert.Equal(64, createResponse.Data.Hash!.Length);
        Assert.Matches("^[a-f0-9]{64}$", createResponse.Data.Hash);
    }

    [Fact]
    public async Task GeneratesEthereumStyleHashForEthAssets()
    {
        // Create vault
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("Test Vault");
        Assert.NotNull(vaultResponse.Data);
        var vaultId = vaultResponse.Data!.Id;

        // Create ETH wallet and get deposit address
        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "ETH");
        Assert.NotNull(walletResponse.Data);
        var depositAddress = walletResponse.Data!.DepositAddress;

        // Create transaction without providing hash
        var createResponse = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = "external-eth-address",
            DestinationAddress = depositAddress,
            Amount = "1.5"
        });

        Assert.NotNull(createResponse.Data);
        Assert.NotNull(createResponse.Data!.Hash);
        Assert.NotEmpty(createResponse.Data.Hash);
        // ETH should generate hash with 0x prefix
        Assert.StartsWith("0x", createResponse.Data.Hash);
        // Should be 0x + 64 hex characters = 66 total
        Assert.Equal(66, createResponse.Data.Hash!.Length);
        Assert.Matches("^0x[a-f0-9]{64}$", createResponse.Data.Hash);
    }
}
