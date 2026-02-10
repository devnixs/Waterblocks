using FluentAssertions;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class VaultArchivingTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task DeleteVault_ArchivesVault_AndHidesItFromDefaultQueries()
    {
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("ArchiveMe");
        vaultResponse.IsSuccess.Should().BeTrue();
        var vaultId = vaultResponse.Data!.Id;

        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "BTC");
        walletResponse.IsSuccess.Should().BeTrue();

        var archiveResponse = await _fixture.AdminClient.DeleteVaultAsync(vaultId);
        archiveResponse.IsSuccess.Should().BeTrue();
        archiveResponse.Data.Should().BeTrue();

        var getArchivedVault = await _fixture.AdminClient.GetVaultAsync(vaultId);
        getArchivedVault.IsSuccess.Should().BeFalse();
        getArchivedVault.Error!.Code.Should().Be("VAULT_NOT_FOUND");

        var activeVaults = await _fixture.AdminClient.GetVaultsAsync();
        activeVaults.IsSuccess.Should().BeTrue();
        activeVaults.Data!.Should().NotContain(v => v.Id == vaultId);

        var allVaults = await _fixture.AdminClient.GetVaultsAsync(includeArchived: true);
        allVaults.IsSuccess.Should().BeTrue();
        allVaults.Data!.Should().Contain(v => v.Id == vaultId);
    }

    [Fact]
    public async Task UnarchiveVault_RestoresVault_AndKeepsWalletData()
    {
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("RestoreMe");
        vaultResponse.IsSuccess.Should().BeTrue();
        var vaultId = vaultResponse.Data!.Id;

        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultId, "ETH");
        walletResponse.IsSuccess.Should().BeTrue();

        var archiveResponse = await _fixture.AdminClient.DeleteVaultAsync(vaultId);
        archiveResponse.IsSuccess.Should().BeTrue();

        var unarchiveResponse = await _fixture.AdminClient.UnarchiveVaultAsync(vaultId);
        unarchiveResponse.IsSuccess.Should().BeTrue();
        unarchiveResponse.Data.Should().BeTrue();

        var restoredVault = await _fixture.AdminClient.GetVaultAsync(vaultId);
        restoredVault.IsSuccess.Should().BeTrue();
        restoredVault.Data!.Wallets.Should().Contain(w => w.AssetId == "ETH");

        var activeVaults = await _fixture.AdminClient.GetVaultsAsync();
        activeVaults.IsSuccess.Should().BeTrue();
        activeVaults.Data!.Should().Contain(v => v.Id == vaultId);
    }
}
