using Waterblocks.Api.Services;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class AddressGenerationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IAddressGenerator _generator;
    private readonly IAddressValidationService _validator;

    public AddressGenerationTests(IntegrationTestFixture fixture)
    {
        _generator = fixture.GetRequiredService<IAddressGenerator>();
        _validator = fixture.GetRequiredService<IAddressValidationService>();
    }

    [Theory]
    [InlineData("BTC")]
    [InlineData("ETH")]
    [InlineData("SOL")]
    [InlineData("ADA")]
    [InlineData("BNB_BSC")]
    [InlineData("MATIC_POLYGON")]
    public void GeneratesValidAddresses(string assetId)
    {
        var vaultAddress = _generator.GenerateVaultAddress(assetId, 0);
        Assert.True(_validator.ValidateAddress(assetId, vaultAddress.AddressValue));

        if (!string.IsNullOrEmpty(vaultAddress.LegacyAddress))
        {
            Assert.True(_validator.ValidateAddress(assetId, vaultAddress.LegacyAddress));
        }

        if (!string.IsNullOrEmpty(vaultAddress.EnterpriseAddress))
        {
            Assert.True(_validator.ValidateAddress(assetId, vaultAddress.EnterpriseAddress));
        }

        var externalAddress = _generator.GenerateExternalAddress(assetId);
        Assert.True(_validator.ValidateAddress(assetId, externalAddress));

        var adminAddress = _generator.GenerateAdminDepositAddress(assetId, "vault-1234");
        Assert.True(_validator.ValidateAddress(assetId, adminAddress));
    }
}
