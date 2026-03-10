using FluentAssertions;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class AdminTransactionValidationTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task CreateTransaction_Rejects_Amount_With_More_Than_18_Decimal_Places()
    {
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("Precision Vault");
        vaultResponse.IsSuccess.Should().BeTrue();

        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultResponse.Data!.Id, "BTC");
        walletResponse.IsSuccess.Should().BeTrue();

        var createResponse = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-btc-source",
            DestinationAddress = walletResponse.Data!.DepositAddress,
            Amount = "0.1234567890123456789",
        });

        createResponse.IsSuccess.Should().BeFalse();
        createResponse.Data.Should().BeNull();
        createResponse.Error.Should().NotBeNull();
        createResponse.Error!.Code.Should().Be("INVALID_AMOUNT");
        createResponse.Error.Message.Should().Contain("too many decimal places");
        createResponse.Error.Message.Should().Contain("18");

        var transactionsResponse = await _fixture.AdminClient.GetTransactionsAsync();
        transactionsResponse.IsSuccess.Should().BeTrue();
        transactionsResponse.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateTransaction_Rejects_NetworkFee_With_More_Than_18_Decimal_Places()
    {
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync("Fee Precision Vault");
        vaultResponse.IsSuccess.Should().BeTrue();

        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultResponse.Data!.Id, "BTC");
        walletResponse.IsSuccess.Should().BeTrue();

        var createResponse = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-btc-source",
            DestinationAddress = walletResponse.Data!.DepositAddress,
            Amount = "1.0",
            NetworkFee = "0.0000000000000000001",
        });

        createResponse.IsSuccess.Should().BeFalse();
        createResponse.Data.Should().BeNull();
        createResponse.Error.Should().NotBeNull();
        createResponse.Error!.Code.Should().Be("INVALID_NETWORK_FEE");
        createResponse.Error.Message.Should().Contain("too many decimal places");
        createResponse.Error.Message.Should().Contain("18");

        var transactionsResponse = await _fixture.AdminClient.GetTransactionsAsync();
        transactionsResponse.IsSuccess.Should().BeTrue();
        transactionsResponse.Data.Should().BeEmpty();
    }
}
