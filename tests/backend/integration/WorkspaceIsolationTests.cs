using FluentAssertions;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

/// <summary>
/// Integration tests to verify workspace isolation.
/// Each API key is tied to a workspace and should only see/affect resources in that workspace.
/// </summary>
public class WorkspaceIsolationTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    #region Vault Isolation Tests

    [Fact]
    public async Task Vaults_Created_With_ApiKey_Belong_To_That_Workspace()
    {
        // Arrange: Create a vault using the Fireblocks API with the default workspace's API key
        var vaultName = "TestVault_" + Guid.NewGuid().ToString("N")[..8];

        // Act: Create vault via Fireblocks API
        var vault = await _fixture.FireblocksClient.CreateVaultAccountAsync(new CreateVaultAccountRequest
        {
            Name = vaultName
        });

        // Assert: Vault should be created
        vault.Should().NotBeNull();
        vault!.Name.Should().Be(vaultName);

        // Verify: Vault should be visible from the Admin API for the same workspace
        var adminVault = await _fixture.AdminClient.GetVaultAsync(vault.Id);
        adminVault.IsSuccess.Should().BeTrue("Vault should be accessible from Admin API for the same workspace");
        adminVault.Data!.Name.Should().Be(vaultName);
    }

    [Fact]
    public async Task Listing_Vaults_Returns_Only_Vaults_From_Own_Workspace()
    {
        // Arrange: Create two workspaces with their own API keys
        var (workspace1Id, workspace1ApiKey) = await _fixture.CreateWorkspaceAsync("Workspace1");
        var (workspace2Id, workspace2ApiKey) = await _fixture.CreateWorkspaceAsync("Workspace2");

        var client1 = _fixture.CreateFireblocksClientWithApiKey(workspace1ApiKey);
        var client2 = _fixture.CreateFireblocksClientWithApiKey(workspace2ApiKey);

        // Create vaults in each workspace
        var vault1 = await client1.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "Vault_In_Workspace1" });
        var vault2 = await client2.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "Vault_In_Workspace2" });

        vault1.Should().NotBeNull();
        vault2.Should().NotBeNull();

        // Act: List vaults from each workspace
        var vaultsFromWorkspace1 = await client1.GetVaultAccountsAsync();
        var vaultsFromWorkspace2 = await client2.GetVaultAccountsAsync();

        // Assert: Each workspace should only see its own vaults
        vaultsFromWorkspace1.Should().NotBeNull();
        vaultsFromWorkspace1!.Should().ContainSingle(v => v.Name == "Vault_In_Workspace1");
        vaultsFromWorkspace1.Should().NotContain(v => v.Name == "Vault_In_Workspace2");

        vaultsFromWorkspace2.Should().NotBeNull();
        vaultsFromWorkspace2!.Should().ContainSingle(v => v.Name == "Vault_In_Workspace2");
        vaultsFromWorkspace2.Should().NotContain(v => v.Name == "Vault_In_Workspace1");
    }

    [Fact]
    public async Task Cannot_Access_Vault_From_Another_Workspace()
    {
        // Arrange: Create two workspaces
        var (workspace1Id, workspace1ApiKey) = await _fixture.CreateWorkspaceAsync("Workspace1");
        var (workspace2Id, workspace2ApiKey) = await _fixture.CreateWorkspaceAsync("Workspace2");

        var client1 = _fixture.CreateFireblocksClientWithApiKey(workspace1ApiKey);
        var client2 = _fixture.CreateFireblocksClientWithApiKey(workspace2ApiKey);

        // Create a vault in workspace 1
        var vault = await client1.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "PrivateVault" });
        vault.Should().NotBeNull();

        // Act: Try to access the vault from workspace 2
        var response = await client2.GetVaultAccountRawAsync(vault!.Id);

        // Assert: Should not find the vault (404 or empty result)
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound,
            "Vault from another workspace should not be accessible");
    }

    #endregion

    #region Transaction Isolation Tests

    [Fact]
    public async Task Transactions_Created_In_Workspace_Only_Visible_From_That_Workspace()
    {
        // Arrange: Create two workspaces
        var (workspace1Id, workspace1ApiKey) = await _fixture.CreateWorkspaceAsync("TxWorkspace1");
        var (workspace2Id, workspace2ApiKey) = await _fixture.CreateWorkspaceAsync("TxWorkspace2");

        var client1 = _fixture.CreateFireblocksClientWithApiKey(workspace1ApiKey);
        var client2 = _fixture.CreateFireblocksClientWithApiKey(workspace2ApiKey);
        var adminClient1 = _fixture.CreateAdminClientForWorkspace(workspace1Id);

        // Create vault and wallet in workspace 1
        var vault = await client1.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "TxTestVault" });
        vault.Should().NotBeNull();

        // Create wallet and fund it using admin API
        await adminClient1.CreateWalletAsync(vault!.Id, "BTC");
        await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "EXTERNAL",
            SourceAddress = "external-funder",
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = vault.Id,
            Amount = "1"
        });

        // Create an outgoing transaction in workspace 1 via Fireblocks API
        var txResponse = await client1.CreateTransactionAsync(new FireblocksCreateTransactionRequest
        {
            AssetId = "BTC",
            Source = new FireblocksTransferPeerPath { Type = "VAULT_ACCOUNT", Id = vault.Id },
            Destination = new FireblocksDestinationTransferPeerPath
            {
                Type = "ONE_TIME_ADDRESS",
                OneTimeAddress = new FireblocksOneTimeAddress { Address = "bc1qtest123" }
            },
            Amount = "0.1"
        });

        txResponse.Should().NotBeNull();
        var txId = txResponse!.Id;

        // Act: List transactions from both workspaces
        var txFromWorkspace1 = await client1.GetTransactionsAsync();
        var txFromWorkspace2 = await client2.GetTransactionsAsync();

        // Assert: Transaction should only be visible from workspace 1
        txFromWorkspace1.Should().NotBeNull();
        txFromWorkspace1!.Should().Contain(t => t.Id == txId, "Transaction should be visible from its own workspace");

        txFromWorkspace2.Should().NotBeNull();
        txFromWorkspace2!.Should().NotContain(t => t.Id == txId, "Transaction should NOT be visible from another workspace");
    }

    [Fact]
    public async Task Cannot_Get_Transaction_By_Id_From_Another_Workspace()
    {
        // Arrange: Create two workspaces
        var (workspace1Id, workspace1ApiKey) = await _fixture.CreateWorkspaceAsync("TxIsoWorkspace1");
        var (workspace2Id, workspace2ApiKey) = await _fixture.CreateWorkspaceAsync("TxIsoWorkspace2");

        var client1 = _fixture.CreateFireblocksClientWithApiKey(workspace1ApiKey);
        var client2 = _fixture.CreateFireblocksClientWithApiKey(workspace2ApiKey);
        var adminClient1 = _fixture.CreateAdminClientForWorkspace(workspace1Id);

        // Create vault and fund it in workspace 1
        var vault = await client1.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "IsoTestVault" });
        await adminClient1.CreateWalletAsync(vault!.Id, "BTC");
        await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "EXTERNAL",
            SourceAddress = "external-funder",
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = vault.Id,
            Amount = "1"
        });

        // Create transaction in workspace 1
        var txResponse = await client1.CreateTransactionAsync(new FireblocksCreateTransactionRequest
        {
            AssetId = "BTC",
            Source = new FireblocksTransferPeerPath { Type = "VAULT_ACCOUNT", Id = vault.Id },
            Destination = new FireblocksDestinationTransferPeerPath
            {
                Type = "ONE_TIME_ADDRESS",
                OneTimeAddress = new FireblocksOneTimeAddress { Address = "bc1qtest456" }
            },
            Amount = "0.1"
        });

        // Act: Try to get the transaction from workspace 2
        var response = await client2.GetTransactionRawAsync(txResponse!.Id);

        // Assert: Should not find the transaction
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound,
            "Transaction from another workspace should not be accessible");
    }

    [Fact]
    public async Task Cannot_Create_Transaction_Using_Vault_From_Another_Workspace()
    {
        // Arrange: Create two workspaces
        var (workspace1Id, workspace1ApiKey) = await _fixture.CreateWorkspaceAsync("SourceWorkspace");
        var (workspace2Id, workspace2ApiKey) = await _fixture.CreateWorkspaceAsync("AttackerWorkspace");

        var client1 = _fixture.CreateFireblocksClientWithApiKey(workspace1ApiKey);
        var client2 = _fixture.CreateFireblocksClientWithApiKey(workspace2ApiKey);
        var adminClient1 = _fixture.CreateAdminClientForWorkspace(workspace1Id);

        // Create vault in workspace 1 and fund it
        var vault = await client1.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "VictimVault" });
        await adminClient1.CreateWalletAsync(vault!.Id, "BTC");
        await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "EXTERNAL",
            SourceAddress = "external-funder",
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = vault.Id,
            Amount = "10"
        });

        // Act: Try to create a transaction from workspace 2 using workspace 1's vault
        Func<Task> act = async () => await client2.CreateTransactionAsync(new FireblocksCreateTransactionRequest
        {
            AssetId = "BTC",
            Source = new FireblocksTransferPeerPath { Type = "VAULT_ACCOUNT", Id = vault.Id },
            Destination = new FireblocksDestinationTransferPeerPath
            {
                Type = "ONE_TIME_ADDRESS",
                OneTimeAddress = new FireblocksOneTimeAddress { Address = "bc1qattacker" }
            },
            Amount = "1"
        });

        // Assert: Should fail because the vault doesn't belong to workspace 2
        await act.Should().ThrowAsync<HttpRequestException>("Cannot use a vault from another workspace");
    }

    #endregion

    #region Admin API Isolation Tests

    [Fact]
    public async Task Admin_Api_Returns_Only_Vaults_For_Current_Workspace()
    {
        // Arrange: Create two workspaces
        var (workspace1Id, workspace1ApiKey) = await _fixture.CreateWorkspaceAsync("AdminWorkspace1");
        var (workspace2Id, workspace2ApiKey) = await _fixture.CreateWorkspaceAsync("AdminWorkspace2");

        var adminClient1 = _fixture.CreateAdminClientForWorkspace(workspace1Id);
        var adminClient2 = _fixture.CreateAdminClientForWorkspace(workspace2Id);

        // Create vaults in each workspace via Admin API
        var vault1Response = await adminClient1.CreateVaultAsync("AdminVault1");
        var vault2Response = await adminClient2.CreateVaultAsync("AdminVault2");

        vault1Response.IsSuccess.Should().BeTrue();
        vault2Response.IsSuccess.Should().BeTrue();

        // Act: List vaults from each admin client
        var vaultsFromAdmin1 = await adminClient1.GetVaultsAsync();
        var vaultsFromAdmin2 = await adminClient2.GetVaultsAsync();

        // Assert: Each workspace should only see its own vaults
        vaultsFromAdmin1.IsSuccess.Should().BeTrue();
        vaultsFromAdmin1.Data.Should().ContainSingle(v => v.Name == "AdminVault1");
        vaultsFromAdmin1.Data.Should().NotContain(v => v.Name == "AdminVault2");

        vaultsFromAdmin2.IsSuccess.Should().BeTrue();
        vaultsFromAdmin2.Data.Should().ContainSingle(v => v.Name == "AdminVault2");
        vaultsFromAdmin2.Data.Should().NotContain(v => v.Name == "AdminVault1");
    }

    [Fact]
    public async Task Admin_Api_Returns_Only_Transactions_For_Current_Workspace()
    {
        // Arrange: Create two workspaces with vaults and transactions
        var (workspace1Id, _) = await _fixture.CreateWorkspaceAsync("AdminTxWorkspace1");
        var (workspace2Id, _) = await _fixture.CreateWorkspaceAsync("AdminTxWorkspace2");

        var adminClient1 = _fixture.CreateAdminClientForWorkspace(workspace1Id);
        var adminClient2 = _fixture.CreateAdminClientForWorkspace(workspace2Id);

        // Create vault and incoming transaction in workspace 1
        var vault1Response = await adminClient1.CreateVaultAsync("TxVault1");
        await adminClient1.CreateWalletAsync(vault1Response.Data!.Id, "BTC");
        var tx1Response = await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "EXTERNAL",
            SourceAddress = "external1",
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = vault1Response.Data.Id,
            Amount = "1"
        });

        // Create vault and incoming transaction in workspace 2
        var vault2Response = await adminClient2.CreateVaultAsync("TxVault2");
        await adminClient2.CreateWalletAsync(vault2Response.Data!.Id, "BTC");
        var tx2Response = await adminClient2.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "EXTERNAL",
            SourceAddress = "external2",
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = vault2Response.Data.Id,
            Amount = "2"
        });

        tx1Response.IsSuccess.Should().BeTrue();
        tx2Response.IsSuccess.Should().BeTrue();

        // Act: List transactions from each workspace
        var txFromWorkspace1 = await adminClient1.GetTransactionsAsync();
        var txFromWorkspace2 = await adminClient2.GetTransactionsAsync();

        // Assert: Each workspace should only see its own transactions
        txFromWorkspace1.IsSuccess.Should().BeTrue();
        txFromWorkspace1.Data.Should().Contain(t => t.Id == tx1Response.Data!.Id);
        txFromWorkspace1.Data.Should().NotContain(t => t.Id == tx2Response.Data!.Id);

        txFromWorkspace2.IsSuccess.Should().BeTrue();
        txFromWorkspace2.Data.Should().Contain(t => t.Id == tx2Response.Data!.Id);
        txFromWorkspace2.Data.Should().NotContain(t => t.Id == tx1Response.Data!.Id);
    }

    #endregion

    #region Invalid API Key Tests

    [Fact]
    public async Task Request_With_Invalid_ApiKey_Returns_Unauthorized()
    {
        // Arrange: Create a client with an invalid API key
        var client = _fixture.CreateFireblocksClientWithApiKey("invalid-api-key-12345");

        // Act & Assert: Request should be rejected
        var response = await client.GetTransactionsRawAsync();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_Without_ApiKey_Returns_Unauthorized()
    {
        // Arrange: Create a client without API key
        var client = _fixture.CreateFireblocksClientWithApiKey("");
        client.ClearApiKey();

        // Act
        var response = await client.GetTransactionsRawAsync();

        // Assert: Should be unauthorized
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Cross-Workspace Transaction Tests

    /// <summary>
    /// Tests that cross-workspace transactions are properly isolated.
    /// When Vault A in Workspace 1 sends to Vault B in Workspace 2:
    /// - Workspace 1 sees: outgoing transaction (Vault A -> external)
    /// - Workspace 2 sees: incoming transaction (external -> Vault B)
    ///
    /// This is simulated by creating paired transactions - an outgoing in workspace 1
    /// and an incoming in workspace 2 using the same destination address.
    /// </summary>
    [Fact]
    public async Task Cross_Workspace_Transaction_Creates_Paired_Transactions_In_Each_Workspace()
    {
        // Arrange: Create two workspaces
        var (workspace1Id, workspace1ApiKey) = await _fixture.CreateWorkspaceAsync("SenderWorkspace");
        var (workspace2Id, workspace2ApiKey) = await _fixture.CreateWorkspaceAsync("ReceiverWorkspace");

        var fireblocksClient1 = _fixture.CreateFireblocksClientWithApiKey(workspace1ApiKey);
        var fireblocksClient2 = _fixture.CreateFireblocksClientWithApiKey(workspace2ApiKey);
        var adminClient1 = _fixture.CreateAdminClientForWorkspace(workspace1Id);
        var adminClient2 = _fixture.CreateAdminClientForWorkspace(workspace2Id);

        // Create vaults in each workspace
        var senderVault = await fireblocksClient1.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "SenderVault" });
        var receiverVault = await fireblocksClient2.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "ReceiverVault" });

        senderVault.Should().NotBeNull();
        receiverVault.Should().NotBeNull();

        // Create wallets for both vaults
        await adminClient1.CreateWalletAsync(senderVault!.Id, "BTC");
        var receiverWalletResponse = await adminClient2.CreateWalletAsync(receiverVault!.Id, "BTC");
        receiverWalletResponse.IsSuccess.Should().BeTrue();

        // Get the receiver's deposit address
        var receiverVaultDetails = await adminClient2.GetVaultAsync(receiverVault.Id);
        var receiverDepositAddress = receiverVaultDetails.Data!.Wallets
            .First(w => w.AssetId == "BTC").DepositAddress;

        // Fund the sender vault
        var fundingTx = await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "EXTERNAL",
            SourceAddress = "external-funder",
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = senderVault.Id,
            Amount = "10"
        });
        fundingTx.IsSuccess.Should().BeTrue();

        // Simulate cross-workspace transfer by creating paired transactions:
        // 1. Outgoing transaction in workspace 1 (sender -> external address)
        var outgoingTx = await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "INTERNAL",
            SourceVaultAccountId = senderVault.Id,
            DestinationType = "EXTERNAL",
            DestinationAddress = receiverDepositAddress,
            Amount = "1"
        });
        outgoingTx.IsSuccess.Should().BeTrue("Outgoing transaction should be created");

        // Complete the outgoing transaction
        await adminClient1.CompleteTransactionFullCycleAsync(outgoingTx.Data!.Id);

        // 2. Incoming transaction in workspace 2 (external -> receiver vault)
        var incomingTx = await adminClient2.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "EXTERNAL",
            SourceAddress = "external-sender",  // Would be sender's address in real scenario
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = receiverVault.Id,
            Amount = "1"
        });
        incomingTx.IsSuccess.Should().BeTrue("Incoming transaction should be created");

        // Act: Query transactions from each workspace
        var workspace1Transactions = await adminClient1.GetTransactionsAsync();
        var workspace2Transactions = await adminClient2.GetTransactionsAsync();

        // Assert: Workspace 1 sees outgoing transaction
        workspace1Transactions.IsSuccess.Should().BeTrue();
        workspace1Transactions.Data.Should().Contain(t => t.Id == outgoingTx.Data!.Id,
            "Workspace 1 should see the outgoing transaction");
        workspace1Transactions.Data.Should().NotContain(t => t.Id == incomingTx.Data!.Id,
            "Workspace 1 should NOT see the incoming transaction from workspace 2");

        var ws1OutgoingTx = workspace1Transactions.Data!.First(t => t.Id == outgoingTx.Data!.Id);
        ws1OutgoingTx.SourceType.Should().Be("INTERNAL", "Source should be internal (vault)");
        ws1OutgoingTx.DestinationType.Should().Be("EXTERNAL", "Destination should be external");
        ws1OutgoingTx.SourceVaultAccountId.Should().Be(senderVault.Id);

        // Assert: Workspace 2 sees incoming transaction
        workspace2Transactions.IsSuccess.Should().BeTrue();
        workspace2Transactions.Data.Should().Contain(t => t.Id == incomingTx.Data!.Id,
            "Workspace 2 should see the incoming transaction");
        workspace2Transactions.Data.Should().NotContain(t => t.Id == outgoingTx.Data!.Id,
            "Workspace 2 should NOT see the outgoing transaction from workspace 1");

        var ws2IncomingTx = workspace2Transactions.Data!.First(t => t.Id == incomingTx.Data!.Id);
        ws2IncomingTx.SourceType.Should().Be("EXTERNAL", "Source should be external");
        ws2IncomingTx.DestinationType.Should().Be("INTERNAL", "Destination should be internal (vault)");
        ws2IncomingTx.DestinationVaultAccountId.Should().Be(receiverVault.Id);
    }

    [Fact]
    public async Task Cross_Workspace_Transactions_Via_Fireblocks_Api_Are_Isolated()
    {
        // Arrange: Create two workspaces
        var (workspace1Id, workspace1ApiKey) = await _fixture.CreateWorkspaceAsync("FbSenderWorkspace");
        var (workspace2Id, workspace2ApiKey) = await _fixture.CreateWorkspaceAsync("FbReceiverWorkspace");

        var fireblocksClient1 = _fixture.CreateFireblocksClientWithApiKey(workspace1ApiKey);
        var fireblocksClient2 = _fixture.CreateFireblocksClientWithApiKey(workspace2ApiKey);
        var adminClient1 = _fixture.CreateAdminClientForWorkspace(workspace1Id);

        // Create sender vault and wallet via Fireblocks API
        var senderVault = await fireblocksClient1.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "FbSenderVault" });
        await fireblocksClient1.CreateWalletAsync(senderVault!.Id, "BTC");

        // Fund the sender vault via Admin API
        await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "EXTERNAL",
            SourceAddress = "funder",
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = senderVault.Id,
            Amount = "5"
        });

        // Create receiver vault and wallet via Fireblocks API (generates valid BTC address)
        var receiverVault = await fireblocksClient2.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "FbReceiverVault" });
        var receiverWallet = await fireblocksClient2.CreateWalletAsync(receiverVault!.Id, "BTC");
        var receiverAddress = receiverWallet!.Address;

        // Create outgoing transaction via Fireblocks API (to external address)
        var txResponse = await fireblocksClient1.CreateTransactionAsync(new FireblocksCreateTransactionRequest
        {
            AssetId = "BTC",
            Source = new FireblocksTransferPeerPath { Type = "VAULT_ACCOUNT", Id = senderVault.Id },
            Destination = new FireblocksDestinationTransferPeerPath
            {
                Type = "ONE_TIME_ADDRESS",
                OneTimeAddress = new FireblocksOneTimeAddress { Address = receiverAddress }
            },
            Amount = "0.5"
        });
        txResponse.Should().NotBeNull();

        // Act: Query transactions from both workspaces via Fireblocks API
        var txFromWorkspace1 = await fireblocksClient1.GetTransactionsAsync();
        var txFromWorkspace2 = await fireblocksClient2.GetTransactionsAsync();

        // Assert: Transaction is only visible from workspace 1 (the sender's workspace)
        txFromWorkspace1.Should().NotBeNull();
        txFromWorkspace1!.Should().Contain(t => t.Id == txResponse!.Id,
            "Outgoing transaction should be visible from sender's workspace");

        txFromWorkspace2.Should().NotBeNull();
        txFromWorkspace2!.Should().NotContain(t => t.Id == txResponse!.Id,
            "Transaction should NOT be visible from receiver's workspace until an incoming tx is created");

        // The receiver workspace would see an incoming transaction only if the blockchain
        // simulator detects the transfer and creates one, or if it's manually created
    }

    [Fact]
    public async Task Fireblocks_Api_Transaction_To_Other_Workspace_Vault_Address_Shows_As_External()
    {
        // Arrange: Create two workspaces
        var (workspace1Id, workspace1ApiKey) = await _fixture.CreateWorkspaceAsync("OutWorkspace");
        var (workspace2Id, workspace2ApiKey) = await _fixture.CreateWorkspaceAsync("InWorkspace");

        var fireblocksClient1 = _fixture.CreateFireblocksClientWithApiKey(workspace1ApiKey);
        var fireblocksClient2 = _fixture.CreateFireblocksClientWithApiKey(workspace2ApiKey);
        var adminClient1 = _fixture.CreateAdminClientForWorkspace(workspace1Id);

        // Create sender vault and wallet via Fireblocks API
        var senderVault = await fireblocksClient1.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "OutVault" });
        await fireblocksClient1.CreateWalletAsync(senderVault!.Id, "BTC");

        // Fund the sender vault via Admin API
        await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceType = "EXTERNAL",
            SourceAddress = "funder",
            DestinationType = "INTERNAL",
            DestinationVaultAccountId = senderVault.Id,
            Amount = "2"
        });

        // Create receiver vault and wallet via Fireblocks API (generates valid BTC address)
        var receiverVault = await fireblocksClient2.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "InVault" });
        var receiverWallet = await fireblocksClient2.CreateWalletAsync(receiverVault!.Id, "BTC");
        var receiverAddress = receiverWallet!.Address;

        // Create transaction to the receiver's address
        var txResponse = await fireblocksClient1.CreateTransactionAsync(new FireblocksCreateTransactionRequest
        {
            AssetId = "BTC",
            Source = new FireblocksTransferPeerPath { Type = "VAULT_ACCOUNT", Id = senderVault.Id },
            Destination = new FireblocksDestinationTransferPeerPath
            {
                Type = "ONE_TIME_ADDRESS",
                OneTimeAddress = new FireblocksOneTimeAddress { Address = receiverAddress }
            },
            Amount = "0.25"
        });

        // Act: Get the transaction details
        var tx = await fireblocksClient1.GetTransactionAsync(txResponse!.Id);

        // Assert: The destination should be ONE_TIME_ADDRESS (external), not VAULT_ACCOUNT
        tx.Should().NotBeNull();
        tx!.Destination.Should().NotBeNull();
        tx.Destination!.Type.Should().Be("ONE_TIME_ADDRESS",
            "Destination should be external even though the address belongs to a vault in another workspace");
        tx.DestinationAddress.Should().Be(receiverAddress,
            "Destination address should be the receiver vault's deposit address");
    }

    #endregion
}
