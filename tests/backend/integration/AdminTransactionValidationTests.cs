using System.Globalization;
using FluentAssertions;
using Waterblocks.Api.Utils;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class AdminTransactionValidationTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture = new();
    private const string ExternalToExternalMessage =
        "You are trying to create a transaction from an external address to another external address. Are you sure the destination address exists?";
    private const string UsdtAssetId = "USDT_ERC20";
    private static readonly string LargestReadableAmount =
        DecimalColumnGuard.MaxReadableValue.ToString(CultureInfo.InvariantCulture);

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task CreateTransaction_Rejects_External_To_External_Scope_With_Required_Message()
    {
        var createResponse = await _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-btc-source",
            DestinationAddress = "external-btc-destination",
            Amount = "1.0",
        });

        createResponse.IsSuccess.Should().BeFalse();
        createResponse.Data.Should().BeNull();
        createResponse.Error.Should().NotBeNull();
        createResponse.Error!.Code.Should().Be("INVALID_TRANSACTION_SCOPE");
        createResponse.Error.Message.Should().Be(ExternalToExternalMessage);

        var transactionsResponse = await _fixture.AdminClient.GetTransactionsAsync();
        transactionsResponse.IsSuccess.Should().BeTrue();
        transactionsResponse.Data.Should().BeEmpty();
    }

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

    [Fact]
    public async Task CreateTransaction_Rejects_Deposit_Above_The_Largest_Readable_Value()
    {
        var wallet = await CreateUsdtWalletAsync("Large Deposit Vault");

        var createResponse = await CreateUsdtDepositAsync(wallet.DepositAddress, "1000000000000000");

        createResponse.IsSuccess.Should().BeFalse();
        createResponse.Error!.Code.Should().Be("INVALID_AMOUNT");
        createResponse.Error.Message.Should().Be($"Amount is too large. Maximum supported value is {LargestReadableAmount}.");

        var transactionsResponse = await _fixture.AdminClient.GetTransactionsAsync();
        transactionsResponse.IsSuccess.Should().BeTrue(transactionsResponse.Error?.Message);
        transactionsResponse.Data.Should().BeEmpty();
        (await GetUsdtBalanceAsync(wallet.VaultId)).Should().Be(0m);
    }

    [Fact]
    public async Task CreateTransaction_Accepts_The_Largest_Readable_Deposit_And_Reads_It_Back()
    {
        var wallet = await CreateUsdtWalletAsync("Largest Deposit Vault");

        var createResponse = await CreateUsdtDepositAsync(wallet.DepositAddress, LargestReadableAmount);

        createResponse.IsSuccess.Should().BeTrue(createResponse.Error?.Message);
        var transactionsResponse = await _fixture.AdminClient.GetTransactionsAsync();
        transactionsResponse.IsSuccess.Should().BeTrue(transactionsResponse.Error?.Message);
        decimal.Parse(transactionsResponse.Data!.Single().Amount, CultureInfo.InvariantCulture)
            .Should().Be(DecimalColumnGuard.MaxReadableValue);
        (await GetUsdtBalanceAsync(wallet.VaultId)).Should().Be(DecimalColumnGuard.MaxReadableValue);
    }

    [Fact]
    public async Task CreateTransaction_Rejects_Deposit_That_Would_Make_The_Balance_Unreadable()
    {
        var wallet = await CreateUsdtWalletAsync("Full Balance Vault");
        var firstDeposit = await CreateUsdtDepositAsync(wallet.DepositAddress, LargestReadableAmount);
        firstDeposit.IsSuccess.Should().BeTrue(firstDeposit.Error?.Message);

        var secondDeposit = await CreateUsdtDepositAsync(wallet.DepositAddress, "1");

        secondDeposit.IsSuccess.Should().BeFalse();
        secondDeposit.Error!.Code.Should().Be("UNSAFE_DECIMAL_VALUE");
        secondDeposit.Error.Message.Should().Contain($"Maximum supported value is {LargestReadableAmount}.");

        var transactionsResponse = await _fixture.AdminClient.GetTransactionsAsync();
        transactionsResponse.IsSuccess.Should().BeTrue(transactionsResponse.Error?.Message);
        transactionsResponse.Data.Should().ContainSingle();
        (await GetUsdtBalanceAsync(wallet.VaultId)).Should().Be(DecimalColumnGuard.MaxReadableValue);
    }

    private async Task<(string VaultId, string DepositAddress)> CreateUsdtWalletAsync(string vaultName)
    {
        var vaultResponse = await _fixture.AdminClient.CreateVaultAsync(vaultName);
        vaultResponse.IsSuccess.Should().BeTrue(vaultResponse.Error?.Message);

        var walletResponse = await _fixture.AdminClient.CreateWalletAsync(vaultResponse.Data!.Id, UsdtAssetId);
        walletResponse.IsSuccess.Should().BeTrue(walletResponse.Error?.Message);

        return (vaultResponse.Data.Id, walletResponse.Data!.DepositAddress!);
    }

    private Task<AdminResponse<TransactionDto>> CreateUsdtDepositAsync(string depositAddress, string amount)
    {
        return _fixture.AdminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = UsdtAssetId,
            DestinationAddress = depositAddress,
            Amount = amount,
        });
    }

    private async Task<decimal> GetUsdtBalanceAsync(string vaultId)
    {
        var vaultResponse = await _fixture.AdminClient.GetVaultAsync(vaultId);
        vaultResponse.IsSuccess.Should().BeTrue(vaultResponse.Error?.Message);

        var wallet = vaultResponse.Data!.Wallets.Single(item => item.AssetId == UsdtAssetId);
        return decimal.Parse(wallet.Balance, CultureInfo.InvariantCulture);
    }
}
