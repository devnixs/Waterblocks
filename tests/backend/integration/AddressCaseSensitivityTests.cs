using System.Globalization;
using FluentAssertions;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class AddressCaseSensitivityTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Admin_Incoming_To_Known_Evm_Address_With_Different_Casing_Is_Internal_And_Credits_Vault()
    {
        var (workspaceId, _) = await _fixture.CreateWorkspaceAsync("CaseAdminWorkspace");
        var admin = _fixture.CreateAdminClientForWorkspace(workspaceId);

        var vaultResponse = await admin.CreateVaultAsync("CaseVault");
        vaultResponse.IsSuccess.Should().BeTrue();

        var walletResponse = await admin.CreateWalletAsync(vaultResponse.Data!.Id, "ETH");
        walletResponse.IsSuccess.Should().BeTrue();
        var canonicalAddress = walletResponse.Data!.DepositAddress;
        canonicalAddress.Should().NotBeNullOrWhiteSpace();

        var destinationAddress = SwapAddressCasing(canonicalAddress!);
        destinationAddress.Should().NotBe(canonicalAddress);

        var createResponse = await admin.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = "0x1111111111111111111111111111111111111111",
            DestinationAddress = destinationAddress,
            Amount = "1.5",
        });

        createResponse.IsSuccess.Should().BeTrue();

        var txId = createResponse.Data!.Id;
        var transactionResponse = await admin.GetTransactionAsync(txId);
        transactionResponse.IsSuccess.Should().BeTrue();
        transactionResponse.Data!.DestinationType.Should().Be("INTERNAL");

        var vaultDetails = await admin.GetVaultAsync(vaultResponse.Data.Id);
        vaultDetails.IsSuccess.Should().BeTrue();

        var ethWallet = vaultDetails.Data!.Wallets.First(w => w.AssetId == "ETH");
        decimal.Parse(ethWallet.Balance, CultureInfo.InvariantCulture).Should().Be(1.5m);
    }

    [Fact]
    public async Task Fireblocks_Transaction_To_Known_Evm_Address_With_Different_Casing_Is_Visible_As_Internal_For_Receiver()
    {
        var (senderWorkspaceId, senderApiKey) = await _fixture.CreateWorkspaceAsync("CaseSenderWorkspace");
        var (receiverWorkspaceId, receiverApiKey) = await _fixture.CreateWorkspaceAsync("CaseReceiverWorkspace");

        var senderFireblocks = _fixture.CreateFireblocksClientWithApiKey(senderApiKey);
        var receiverFireblocks = _fixture.CreateFireblocksClientWithApiKey(receiverApiKey);
        var senderAdmin = _fixture.CreateAdminClientForWorkspace(senderWorkspaceId);
        var receiverAdmin = _fixture.CreateAdminClientForWorkspace(receiverWorkspaceId);

        var senderVault = await senderFireblocks.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "CaseSenderVault" });
        var receiverVault = await receiverFireblocks.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "CaseReceiverVault" });
        senderVault.Should().NotBeNull();
        receiverVault.Should().NotBeNull();

        var senderWallet = await senderFireblocks.CreateWalletAsync(senderVault!.Id, "ETH");
        var receiverWallet = await receiverFireblocks.CreateWalletAsync(receiverVault!.Id, "ETH");
        senderWallet.Should().NotBeNull();
        receiverWallet.Should().NotBeNull();

        var senderAddress = senderWallet!.Address;
        var receiverCanonicalAddress = receiverWallet!.Address;
        var receiverTypedAddress = SwapAddressCasing(receiverCanonicalAddress);

        var fundResponse = await senderAdmin.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "ETH",
            SourceAddress = "0x2222222222222222222222222222222222222222",
            DestinationAddress = senderAddress,
            Amount = "5",
        });
        fundResponse.IsSuccess.Should().BeTrue();

        var createResponse = await senderFireblocks.CreateTransactionAsync(new FireblocksCreateTransactionRequest
        {
            AssetId = "ETH",
            Source = new FireblocksTransferPeerPath { Type = "VAULT_ACCOUNT", Id = senderVault.Id },
            Destination = new FireblocksDestinationTransferPeerPath
            {
                Type = "ONE_TIME_ADDRESS",
                OneTimeAddress = new FireblocksOneTimeAddress { Address = receiverTypedAddress },
            },
            Amount = "2",
        });
        createResponse.Should().NotBeNull();

        var receiverTransactions = await receiverFireblocks.GetTransactionsAsync();
        receiverTransactions.Should().NotBeNull();
        receiverTransactions!.Should().Contain(t => t.DestinationAddress == receiverTypedAddress);

        var receiverPerspective = receiverTransactions!.First(t => t.DestinationAddress == receiverTypedAddress);
        receiverPerspective.Destination!.Type.Should().Be("VAULT_ACCOUNT");

        var completeResponse = await senderAdmin.CompleteTransactionFullCycleAsync(createResponse!.Id);
        completeResponse.IsSuccess.Should().BeTrue();

        var receiverVaultAfter = await receiverAdmin.GetVaultAsync(receiverVault.Id);
        receiverVaultAfter.IsSuccess.Should().BeTrue();
        var receiverEthWallet = receiverVaultAfter.Data!.Wallets.First(w => w.AssetId == "ETH");
        decimal.Parse(receiverEthWallet.Balance, CultureInfo.InvariantCulture).Should().Be(2m);
    }

    private static string SwapAddressCasing(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return address;
        }

        var chars = address.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetter(chars[i]))
            {
                continue;
            }

            chars[i] = char.IsUpper(chars[i])
                ? char.ToLowerInvariant(chars[i])
                : char.ToUpperInvariant(chars[i]);
        }

        return new string(chars);
    }
}
