using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class AdminAssetTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AdminAssetTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreatesUpdatesAndDeactivatesAssets()
    {
        var assetId = $"TEST_{Guid.NewGuid():N}".ToUpperInvariant()[..13];

        var createResponse = await _fixture.AdminClient.CreateAssetAsync(new CreateAdminAssetRequest
        {
            AssetId = assetId,
            Name = "Test Asset",
            Symbol = "TST",
            Decimals = 6,
            Type = "ERC20",
            BlockchainType = "AccountBased",
            NativeAsset = "ETH",
            BaseFee = 0.001m,
            FeeAssetId = "ETH",
            IsActive = true,
        });

        Assert.NotNull(createResponse.Data);
        Assert.Equal(assetId, createResponse.Data!.Id);
        Assert.Equal("Test Asset", createResponse.Data.Name);

        var updateResponse = await _fixture.AdminClient.UpdateAssetAsync(assetId, new UpdateAdminAssetRequest
        {
            Name = "Test Asset Updated",
            Symbol = "TST2",
            Decimals = 8,
            Type = "BASE_ASSET",
            BlockchainType = "AddressBased",
            BaseFee = 0.002m,
        });

        Assert.NotNull(updateResponse.Data);
        Assert.Equal("Test Asset Updated", updateResponse.Data!.Name);
        Assert.Equal("TST2", updateResponse.Data.Symbol);
        Assert.Equal("AddressBased", updateResponse.Data.BlockchainType);

        var listResponse = await _fixture.AdminClient.GetAssetsAsync();
        Assert.NotNull(listResponse.Data);
        var listedAsset = listResponse.Data!.FirstOrDefault(a => a.Id == assetId);
        Assert.NotNull(listedAsset);
        Assert.Equal("BASE_ASSET", listedAsset!.Type);
        Assert.Equal(0.002m, listedAsset.BaseFee);

        var deleteResponse = await _fixture.AdminClient.DeleteAssetAsync(assetId);
        Assert.True(deleteResponse.Data);

        var listAfterDelete = await _fixture.AdminClient.GetAssetsAsync();
        var deletedAsset = listAfterDelete.Data!.First(a => a.Id == assetId);
        Assert.False(deletedAsset.IsActive);
    }
}
