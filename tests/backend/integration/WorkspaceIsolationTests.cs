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
        var vaultDetails = await adminClient1.GetVaultAsync(vault.Id);
        var vaultDepositAddress = vaultDetails.Data!.Wallets.First(w => w.AssetId == "BTC").DepositAddress;
        await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-funder",
            DestinationAddress = vaultDepositAddress,
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
        var vaultDetails = await adminClient1.GetVaultAsync(vault.Id);
        var vaultDepositAddress = vaultDetails.Data!.Wallets.First(w => w.AssetId == "BTC").DepositAddress;
        await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-funder",
            DestinationAddress = vaultDepositAddress,
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
        var vaultDetails = await adminClient1.GetVaultAsync(vault.Id);
        var vaultDepositAddress = vaultDetails.Data!.Wallets.First(w => w.AssetId == "BTC").DepositAddress;
        await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-funder",
            DestinationAddress = vaultDepositAddress,
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
        var vault1Details = await adminClient1.GetVaultAsync(vault1Response.Data!.Id);
        var vault1DepositAddress = vault1Details.Data!.Wallets.First(w => w.AssetId == "BTC").DepositAddress;
        var tx1Response = await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external1",
            DestinationAddress = vault1DepositAddress,
            Amount = "1"
        });

        // Create vault and incoming transaction in workspace 2
        var vault2Response = await adminClient2.CreateVaultAsync("TxVault2");
        await adminClient2.CreateWalletAsync(vault2Response.Data!.Id, "BTC");
        var vault2Details = await adminClient2.GetVaultAsync(vault2Response.Data!.Id);
        var vault2DepositAddress = vault2Details.Data!.Wallets.First(w => w.AssetId == "BTC").DepositAddress;
        var tx2Response = await adminClient2.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external2",
            DestinationAddress = vault2DepositAddress,
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

    [Fact]
    public async Task Cross_Workspace_Transaction_Has_Distinct_Composite_Ids_Per_Workspace()
    {
        // Arrange: Create two workspaces
        var (workspace1Id, workspace1ApiKey) = await _fixture.CreateWorkspaceAsync("CompositeSenderWorkspace");
        var (workspace2Id, workspace2ApiKey) = await _fixture.CreateWorkspaceAsync("CompositeReceiverWorkspace");

        var fireblocksClient1 = _fixture.CreateFireblocksClientWithApiKey(workspace1ApiKey);
        var fireblocksClient2 = _fixture.CreateFireblocksClientWithApiKey(workspace2ApiKey);
        var adminClient1 = _fixture.CreateAdminClientForWorkspace(workspace1Id);
        var adminClient2 = _fixture.CreateAdminClientForWorkspace(workspace2Id);

        // Create vaults in each workspace
        var senderVault = await fireblocksClient1.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "CompositeSenderVault" });
        var receiverVault = await fireblocksClient2.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "CompositeReceiverVault" });

        // Create wallets and get deposit addresses
        var senderWalletResponse = await adminClient1.CreateWalletAsync(senderVault!.Id, "BTC");
        var senderDepositAddress = senderWalletResponse.Data!.DepositAddress;

        await adminClient2.CreateWalletAsync(receiverVault!.Id, "BTC");
        var receiverVaultDetails = await adminClient2.GetVaultAsync(receiverVault!.Id);
        var receiverDepositAddress = receiverVaultDetails.Data!.Wallets.First(w => w.AssetId == "BTC").DepositAddress;

        // Fund the sender vault
        await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-funder",
            DestinationAddress = senderDepositAddress,
            Amount = "10"
        });

        // Create cross-workspace transaction
        var crossWorkspaceTx = await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = senderDepositAddress,
            DestinationAddress = receiverDepositAddress,
            Amount = "1"
        });
        crossWorkspaceTx.IsSuccess.Should().BeTrue();

        var senderCompositeId = crossWorkspaceTx.Data!.Id;

        // Act: List transactions from each workspace
        var workspace1Transactions = await adminClient1.GetTransactionsAsync();
        var workspace2Transactions = await adminClient2.GetTransactionsAsync();

        // Assert: both workspaces should see the same transaction with different composite IDs
        var ws1Tx = workspace1Transactions.Data!.First(t => t.Id == senderCompositeId);
        var ws2Tx = workspace2Transactions.Data!.First(t => t.DestinationVaultAccountName == "CompositeReceiverVault"
                                                            && t.DestinationAddress == receiverDepositAddress);

        ws1Tx.Id.Should().StartWith(workspace1Id + "::");
        ws2Tx.Id.Should().StartWith(workspace2Id + "::");
        ws1Tx.Id.Should().NotBe(ws2Tx.Id, "each workspace should receive a distinct composite ID");

        // And both composite IDs should be resolvable by their respective workspace
        var senderTxById = await adminClient1.GetTransactionAsync(ws1Tx.Id);
        var receiverTxById = await adminClient2.GetTransactionAsync(ws2Tx.Id);
        senderTxById.IsSuccess.Should().BeTrue();
        receiverTxById.IsSuccess.Should().BeTrue();
    }


    /// <summary>
    /// Tests that a single transaction is visible from BOTH workspaces when it involves
    /// addresses owned by each workspace. The same transaction appears with different
    /// source/destination types based on the viewer's perspective:
    /// - Workspace 1 (sender) sees: source=INTERNAL (their vault), destination=EXTERNAL
    /// - Workspace 2 (receiver) sees: source=EXTERNAL, destination=INTERNAL (their vault)
    /// </summary>
    [Fact]
    public async Task Cross_Workspace_Transaction_Visible_From_Both_Workspaces_With_Different_Perspective()
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
        var senderWalletResponse = await adminClient1.CreateWalletAsync(senderVault!.Id, "BTC");
        var senderDepositAddress = senderWalletResponse.Data!.DepositAddress;
        var receiverWalletResponse = await adminClient2.CreateWalletAsync(receiverVault!.Id, "BTC");
        receiverWalletResponse.IsSuccess.Should().BeTrue();

        // Get the receiver's deposit address
        var receiverVaultDetails = await adminClient2.GetVaultAsync(receiverVault.Id);
        var receiverDepositAddress = receiverVaultDetails.Data!.Wallets
            .First(w => w.AssetId == "BTC").DepositAddress;

        // Fund the sender vault
        await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-funder",
            DestinationAddress = senderDepositAddress,
            Amount = "10"
        });

        // Create cross-workspace transaction: sender vault -> receiver vault
        var crossWorkspaceTx = await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = senderDepositAddress,
            DestinationAddress = receiverDepositAddress,
            Amount = "1"
        });
        crossWorkspaceTx.IsSuccess.Should().BeTrue("Cross-workspace transaction should be created");
        var txId = crossWorkspaceTx.Data!.Id;

        // Complete the transaction
        await adminClient1.CompleteTransactionFullCycleAsync(txId);

        // Act: Query transactions from each workspace
        var workspace1Transactions = await adminClient1.GetTransactionsAsync();
        var workspace2Transactions = await adminClient2.GetTransactionsAsync();

        // Assert: BOTH workspaces should see the same transaction (with different composite IDs)
        workspace1Transactions.IsSuccess.Should().BeTrue();
        workspace1Transactions.Data.Should().Contain(t => t.Id == txId,
            "Workspace 1 (sender) should see the cross-workspace transaction");

        workspace2Transactions.IsSuccess.Should().BeTrue();
        workspace2Transactions.Data.Should().Contain(t => t.DestinationVaultAccountName == "ReceiverVault",
            "Workspace 2 (receiver) should see the cross-workspace transaction");

        // Verify workspace 1's perspective (sender)
        var ws1Tx = workspace1Transactions.Data!.First(t => t.Id == txId);
        ws1Tx.SourceType.Should().Be("INTERNAL", "From sender's perspective, source should be INTERNAL (their vault)");
        ws1Tx.DestinationType.Should().Be("EXTERNAL", "From sender's perspective, destination should be EXTERNAL");
        ws1Tx.SourceVaultAccountName.Should().Be("SenderVault");

        // Verify workspace 2's perspective (receiver)
        var ws2Tx = workspace2Transactions.Data!.First(t => t.DestinationVaultAccountName == "ReceiverVault");
        ws2Tx.Id.Should().NotBe(txId, "each workspace should get a distinct composite transaction ID");
        ws2Tx.SourceType.Should().Be("EXTERNAL", "From receiver's perspective, source should be EXTERNAL");
        ws2Tx.DestinationType.Should().Be("INTERNAL", "From receiver's perspective, destination should be INTERNAL (their vault)");
        ws2Tx.DestinationVaultAccountName.Should().Be("ReceiverVault");
    }

    /// <summary>
    /// Tests that a cross-workspace transaction is NOT visible from a third workspace
    /// that doesn't own either the source or destination address.
    /// </summary>
    [Fact]
    public async Task Cross_Workspace_Transaction_Not_Visible_From_Unrelated_Workspace()
    {
        // Arrange: Create three workspaces
        var (workspace1Id, workspace1ApiKey) = await _fixture.CreateWorkspaceAsync("SenderWorkspace3");
        var (workspace2Id, workspace2ApiKey) = await _fixture.CreateWorkspaceAsync("ReceiverWorkspace3");
        var (workspace3Id, workspace3ApiKey) = await _fixture.CreateWorkspaceAsync("UnrelatedWorkspace3");

        var fireblocksClient1 = _fixture.CreateFireblocksClientWithApiKey(workspace1ApiKey);
        var fireblocksClient2 = _fixture.CreateFireblocksClientWithApiKey(workspace2ApiKey);
        var adminClient1 = _fixture.CreateAdminClientForWorkspace(workspace1Id);
        var adminClient2 = _fixture.CreateAdminClientForWorkspace(workspace2Id);
        var adminClient3 = _fixture.CreateAdminClientForWorkspace(workspace3Id);

        // Create vaults in workspaces 1 and 2
        var senderVault = await fireblocksClient1.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "SenderVault3" });
        var receiverVault = await fireblocksClient2.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "ReceiverVault3" });

        // Create wallets
        var senderWalletResponse = await adminClient1.CreateWalletAsync(senderVault!.Id, "BTC");
        var senderDepositAddress = senderWalletResponse.Data!.DepositAddress;
        var receiverWalletResponse = await adminClient2.CreateWalletAsync(receiverVault!.Id, "BTC");
        var receiverVaultDetails = await adminClient2.GetVaultAsync(receiverVault!.Id);
        var receiverDepositAddress = receiverVaultDetails.Data!.Wallets.First(w => w.AssetId == "BTC").DepositAddress;

        // Fund the sender vault
        await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-funder",
            DestinationAddress = senderDepositAddress,
            Amount = "10"
        });

        // Create cross-workspace transaction
        var crossWorkspaceTx = await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = senderDepositAddress,
            DestinationAddress = receiverDepositAddress,
            Amount = "1"
        });
        var txId = crossWorkspaceTx.Data!.Id;

        // Act: Query transactions from workspace 3 (the unrelated workspace)
        var workspace3Transactions = await adminClient3.GetTransactionsAsync();

        // Assert: Workspace 3 should NOT see the transaction
        workspace3Transactions.IsSuccess.Should().BeTrue();
        workspace3Transactions.Data.Should().NotContain(t => t.DestinationVaultAccountName == "ReceiverVault3",
            "Unrelated workspace should NOT see transactions between other workspaces");
    }

    /// <summary>
    /// Tests that the Fireblocks API also shows cross-workspace transactions
    /// to both workspaces based on address ownership.
    /// </summary>
    [Fact]
    public async Task Cross_Workspace_Transaction_Via_Fireblocks_Api_Visible_From_Both_Workspaces()
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
        var senderVaultDetails = await adminClient1.GetVaultAsync(senderVault.Id);
        var senderDepositAddress = senderVaultDetails.Data!.Wallets.First(w => w.AssetId == "BTC").DepositAddress;

        // Fund the sender vault via Admin API
        await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "funder",
            DestinationAddress = senderDepositAddress,
            Amount = "5"
        });

        // Create receiver vault and wallet via Fireblocks API (generates valid BTC address)
        var receiverVault = await fireblocksClient2.CreateVaultAccountAsync(new CreateVaultAccountRequest { Name = "FbReceiverVault" });
        var receiverWallet = await fireblocksClient2.CreateWalletAsync(receiverVault!.Id, "BTC");
        var receiverAddress = receiverWallet!.Address;

        // Create outgoing transaction via Fireblocks API
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
        var txId = txResponse!.Id;

        // Act: Query transactions from both workspaces via Fireblocks API
        var txFromWorkspace1 = await fireblocksClient1.GetTransactionsAsync();
        var txFromWorkspace2 = await fireblocksClient2.GetTransactionsAsync();

        // Assert: Transaction should be visible from BOTH workspaces
        txFromWorkspace1.Should().NotBeNull();
        txFromWorkspace1!.Should().Contain(t => t.Id == txId,
            "Transaction should be visible from sender's workspace");

        txFromWorkspace2.Should().NotBeNull();
        txFromWorkspace2!.Should().Contain(t => t.DestinationAddress == receiverAddress,
            "Transaction should be visible from receiver's workspace (cross-workspace)");

        // Verify different perspectives
        var ws1Tx = txFromWorkspace1!.First(t => t.Id == txId);
        ws1Tx.Source!.Type.Should().Be("VAULT_ACCOUNT", "Sender sees source as their vault");

        var ws2Tx = txFromWorkspace2!.First(t => t.DestinationAddress == receiverAddress);
        ws2Tx.Destination!.Type.Should().Be("VAULT_ACCOUNT", "Receiver sees destination as their vault");
        ws2Tx.Id.Should().NotBe(txId, "receiver should see a distinct composite transaction ID");
    }

    [Fact]
    public async Task Fireblocks_Api_Transaction_Shows_Correct_Perspective_Per_Workspace()
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
        var senderVaultDetails = await adminClient1.GetVaultAsync(senderVault.Id);
        var senderDepositAddress = senderVaultDetails.Data!.Wallets.First(w => w.AssetId == "BTC").DepositAddress;

        // Fund the sender vault via Admin API
        await adminClient1.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "funder",
            DestinationAddress = senderDepositAddress,
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
        var txId = txResponse!.Id;

        // Act: Get the transaction details from sender's perspective
        var txFromSender = await fireblocksClient1.GetTransactionAsync(txId);

        // Act: Get the transaction details from receiver's perspective
        var receiverTransactions = await fireblocksClient2.GetTransactionsAsync();
        receiverTransactions.Should().NotBeNull();
        var receiverCompositeId = receiverTransactions!.First(t => t.DestinationAddress == receiverAddress).Id;
        var txFromReceiver = await fireblocksClient2.GetTransactionAsync(receiverCompositeId);

        // Assert: Sender's perspective
        txFromSender.Should().NotBeNull();
        txFromSender!.Source!.Type.Should().Be("VAULT_ACCOUNT",
            "Sender should see source as VAULT_ACCOUNT (their vault)");
        txFromSender.Destination!.Type.Should().Be("ONE_TIME_ADDRESS",
            "Sender should see destination as ONE_TIME_ADDRESS (external to them)");

        // Assert: Receiver's perspective
        txFromReceiver.Should().NotBeNull();
        txFromReceiver!.Source!.Type.Should().Be("ONE_TIME_ADDRESS",
            "Receiver should see source as ONE_TIME_ADDRESS (external to them)");
        txFromReceiver.Destination!.Type.Should().Be("VAULT_ACCOUNT",
            "Receiver should see destination as VAULT_ACCOUNT (their vault)");
        txFromReceiver.DestinationAddress.Should().Be(receiverAddress,
            "Destination address should be the receiver vault's deposit address");
    }

    #endregion
}
