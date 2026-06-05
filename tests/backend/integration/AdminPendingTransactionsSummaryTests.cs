using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Infrastructure;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

public class AdminPendingTransactionsSummaryTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AdminPendingTransactionsSummaryTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetPendingTransactionsSummary_ReturnsOnlyNonTerminalTransactionsAcrossAllWorkspaces()
    {
        var senderWorkspaceName = $"PendingSender-{Guid.NewGuid():N}"[..20];
        var receiverWorkspaceName = $"PendingReceiver-{Guid.NewGuid():N}"[..22];
        var (senderWorkspaceId, _) = await _fixture.CreateWorkspaceAsync(senderWorkspaceName);
        var (receiverWorkspaceId, _) = await _fixture.CreateWorkspaceAsync(receiverWorkspaceName);

        var senderAdmin = _fixture.CreateAdminClientForWorkspace(senderWorkspaceId);
        var receiverAdmin = _fixture.CreateAdminClientForWorkspace(receiverWorkspaceId);
        var initialSummary = await senderAdmin.GetPendingTransactionsSummaryAsync();
        initialSummary.IsSuccess.Should().BeTrue();

        var senderVault = await senderAdmin.CreateVaultAsync("Sender Vault");
        var receiverVault = await receiverAdmin.CreateVaultAsync("Receiver Vault");
        senderVault.IsSuccess.Should().BeTrue();
        receiverVault.IsSuccess.Should().BeTrue();

        var senderWallet = await senderAdmin.CreateWalletAsync(senderVault.Data!.Id, "BTC");
        var receiverWallet = await receiverAdmin.CreateWalletAsync(receiverVault.Data!.Id, "BTC");
        senderWallet.IsSuccess.Should().BeTrue();
        receiverWallet.IsSuccess.Should().BeTrue();

        var senderAddress = senderWallet.Data!.DepositAddress!;
        var receiverAddress = receiverWallet.Data!.DepositAddress!;
        await SetAddressDescriptionAsync(senderAddress, "Sender primary");
        await SetAddressDescriptionAsync(receiverAddress, "Receiver primary");

        var fundingResponse = await senderAdmin.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-funder",
            DestinationAddress = senderAddress,
            Amount = "10",
        });

        var pendingIncomingResponse = await senderAdmin.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-pending-source",
            DestinationAddress = senderAddress,
            Amount = "2",
            InitialState = "SUBMITTED",
        });

        var crossWorkspaceResponse = await senderAdmin.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = senderAddress,
            DestinationAddress = receiverAddress,
            Amount = "1",
        });

        fundingResponse.IsSuccess.Should().BeTrue();
        pendingIncomingResponse.IsSuccess.Should().BeTrue();
        crossWorkspaceResponse.IsSuccess.Should().BeTrue();

        var summaryResponse = await senderAdmin.GetPendingTransactionsSummaryAsync();

        summaryResponse.IsSuccess.Should().BeTrue();
        summaryResponse.Data.Should().NotBeNull();
        summaryResponse.Data!.Count.Should().Be(initialSummary.Data!.Count + 2);
        summaryResponse.Data.Items.Should().HaveCount(summaryResponse.Data.Count);
        summaryResponse.Data.Items.Should().OnlyContain(item => item.State != "COMPLETED");
        summaryResponse.Data.Items.Select(item => item.Id).Should().Contain(new[]
        {
            pendingIncomingResponse.Data!.Id,
            crossWorkspaceResponse.Data!.Id,
        });

        var crossWorkspaceIndex = summaryResponse.Data.Items.FindIndex(item => item.Id == crossWorkspaceResponse.Data!.Id);
        var pendingIncomingIndex = summaryResponse.Data.Items.FindIndex(item => item.Id == pendingIncomingResponse.Data!.Id);
        crossWorkspaceIndex.Should().BeGreaterOrEqualTo(0);
        pendingIncomingIndex.Should().BeGreaterOrEqualTo(0);
        crossWorkspaceIndex.Should().BeLessThan(pendingIncomingIndex, "newer pending transactions should be ordered first");
        summaryResponse.Data.Items.Should().ContainSingle(item =>
            item.SourceWorkspaceId == senderWorkspaceId &&
            item.DestinationWorkspaceId == receiverWorkspaceId);

        var crossWorkspaceItem = summaryResponse.Data.Items.Single(item => item.Id == crossWorkspaceResponse.Data!.Id);
        crossWorkspaceItem.Amount.Should().Be("1.000000000000000000");
        crossWorkspaceItem.AssetId.Should().Be("BTC");
        crossWorkspaceItem.SourceWorkspaceId.Should().Be(senderWorkspaceId);
        crossWorkspaceItem.SourceWorkspaceName.Should().Be(senderWorkspaceName);
        crossWorkspaceItem.SourceAddressName.Should().Be("Sender primary");
        crossWorkspaceItem.SourceAddress.Should().Be(senderAddress);
        crossWorkspaceItem.DestinationWorkspaceId.Should().Be(receiverWorkspaceId);
        crossWorkspaceItem.DestinationWorkspaceName.Should().Be(receiverWorkspaceName);
        crossWorkspaceItem.DestinationAddressName.Should().Be("Receiver primary");
        crossWorkspaceItem.DestinationAddress.Should().Be(receiverAddress);

        var pendingIncomingItem = summaryResponse.Data.Items.Single(item => item.Id == pendingIncomingResponse.Data!.Id);
        pendingIncomingItem.SourceWorkspaceId.Should().BeNull();
        pendingIncomingItem.SourceWorkspaceName.Should().BeNull();
        pendingIncomingItem.SourceAddressName.Should().BeNull();
        pendingIncomingItem.SourceAddress.Should().Be("external-pending-source");
        pendingIncomingItem.DestinationWorkspaceId.Should().Be(senderWorkspaceId);
        pendingIncomingItem.DestinationWorkspaceName.Should().Be(senderWorkspaceName);
        pendingIncomingItem.DestinationAddressName.Should().Be("Sender primary");
        pendingIncomingItem.DestinationAddress.Should().Be(senderAddress);
    }

    [Fact]
    public async Task PendingTransactionsRealtime_EmitsAdminWideEvent_WhenTransactionsChange()
    {
        using var factory = _fixture.CreateFactory();
        var bootstrapClient = new AdminApiClient(factory.CreateClient());
        var workspaceResponse = await bootstrapClient.CreateWorkspaceAsync($"Realtime-{Guid.NewGuid():N}"[..17]);
        workspaceResponse.IsSuccess.Should().BeTrue();

        var workspaceId = workspaceResponse.Data!.Id;
        var adminClient = new AdminApiClient(factory.CreateClient());
        adminClient.SetWorkspace(workspaceId);

        var vaultResponse = await adminClient.CreateVaultAsync("Realtime Vault");
        vaultResponse.IsSuccess.Should().BeTrue();

        var walletResponse = await adminClient.CreateWalletAsync(vaultResponse.Data!.Id, "BTC");
        walletResponse.IsSuccess.Should().BeTrue();
        var depositAddress = walletResponse.Data!.DepositAddress!;

        var receivedEvents = new ConcurrentQueue<DateTimeOffset>();
        var firstEvent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEvent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = BuildHubConnection(factory);
        connection.On("pendingTransactionsUpdated", () =>
        {
            receivedEvents.Enqueue(DateTimeOffset.UtcNow);
            if (receivedEvents.Count == 1)
            {
                firstEvent.TrySetResult();
            }
            else
            {
                secondEvent.TrySetResult();
            }
        });

        await connection.StartAsync();

        var createResponse = await adminClient.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-realtime-source",
            DestinationAddress = depositAddress,
            Amount = "1",
            InitialState = "SUBMITTED",
        });

        createResponse.IsSuccess.Should().BeTrue();
        await firstEvent.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var cancelResponse = await adminClient.CancelTransactionAsync(createResponse.Data!.Id);
        cancelResponse.IsSuccess.Should().BeTrue();
        await secondEvent.Task.WaitAsync(TimeSpan.FromSeconds(10));

        receivedEvents.Count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task TransactionRealtime_FansOutToEveryParticipatingWorkspace()
    {
        using var factory = _fixture.CreateFactory();
        var bootstrapClient = new AdminApiClient(factory.CreateClient());

        var senderWorkspace = await bootstrapClient.CreateWorkspaceAsync($"TxSender-{Guid.NewGuid():N}"[..17]);
        var receiverWorkspace = await bootstrapClient.CreateWorkspaceAsync($"TxReceiver-{Guid.NewGuid():N}"[..19]);
        senderWorkspace.IsSuccess.Should().BeTrue();
        receiverWorkspace.IsSuccess.Should().BeTrue();

        var senderWorkspaceId = senderWorkspace.Data!.Id;
        var receiverWorkspaceId = receiverWorkspace.Data!.Id;
        var senderAdmin = new AdminApiClient(factory.CreateClient());
        var receiverAdmin = new AdminApiClient(factory.CreateClient());
        senderAdmin.SetWorkspace(senderWorkspaceId);
        receiverAdmin.SetWorkspace(receiverWorkspaceId);

        var senderVault = await senderAdmin.CreateVaultAsync("Sender Vault");
        var receiverVault = await receiverAdmin.CreateVaultAsync("Receiver Vault");
        senderVault.IsSuccess.Should().BeTrue();
        receiverVault.IsSuccess.Should().BeTrue();

        var senderWallet = await senderAdmin.CreateWalletAsync(senderVault.Data!.Id, "BTC");
        var receiverWallet = await receiverAdmin.CreateWalletAsync(receiverVault.Data!.Id, "BTC");
        senderWallet.IsSuccess.Should().BeTrue();
        receiverWallet.IsSuccess.Should().BeTrue();

        var senderTransactionUpsert = new TaskCompletionSource<AdminTransactionDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receiverTransactionUpsert = new TaskCompletionSource<AdminTransactionDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var senderListUpdated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var receiverListUpdated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var senderConnection = BuildHubConnection(factory, senderWorkspaceId);
        senderConnection.On<AdminTransactionDto>("transactionUpserted", dto =>
        {
            if (dto.SourceAddress == senderWallet.Data!.DepositAddress &&
                dto.DestinationAddress == receiverWallet.Data!.DepositAddress &&
                dto.Amount == "1.000000000000000000")
            {
                senderTransactionUpsert.TrySetResult(dto);
            }
        });
        senderConnection.On("transactionsUpdated", () =>
        {
            senderListUpdated.TrySetResult();
        });

        await using var receiverConnection = BuildHubConnection(factory, receiverWorkspaceId);
        receiverConnection.On<AdminTransactionDto>("transactionUpserted", dto =>
        {
            if (dto.SourceAddress == senderWallet.Data!.DepositAddress &&
                dto.DestinationAddress == receiverWallet.Data!.DepositAddress &&
                dto.Amount == "1.000000000000000000")
            {
                receiverTransactionUpsert.TrySetResult(dto);
            }
        });
        receiverConnection.On("transactionsUpdated", () =>
        {
            receiverListUpdated.TrySetResult();
        });

        await senderConnection.StartAsync();
        await receiverConnection.StartAsync();

        var fundingResponse = await senderAdmin.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = "external-funder",
            DestinationAddress = senderWallet.Data!.DepositAddress,
            Amount = "10",
        });
        fundingResponse.IsSuccess.Should().BeTrue();

        var createResponse = await senderAdmin.CreateTransactionAsync(new CreateTransactionRequest
        {
            AssetId = "BTC",
            SourceAddress = senderWallet.Data!.DepositAddress,
            DestinationAddress = receiverWallet.Data!.DepositAddress,
            Amount = "1",
            InitialState = "SUBMITTED",
        });

        createResponse.IsSuccess.Should().BeTrue();

        var senderDto = await senderTransactionUpsert.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var receiverDto = await receiverTransactionUpsert.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await senderListUpdated.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await receiverListUpdated.Task.WaitAsync(TimeSpan.FromSeconds(10));

        TransactionCompositeId.TryParse(createResponse.Data!.Id, out var responseWorkspaceId, out var rawTransactionId).Should().BeTrue();
        responseWorkspaceId.Should().Be(senderWorkspaceId);

        senderDto.Id.Should().Be(TransactionCompositeId.Build(senderWorkspaceId, rawTransactionId));
        receiverDto.Id.Should().Be(TransactionCompositeId.Build(receiverWorkspaceId, rawTransactionId));
        senderDto.DestinationAddress.Should().Be(receiverWallet.Data!.DepositAddress);
        receiverDto.DestinationAddress.Should().Be(receiverWallet.Data!.DepositAddress);
        senderDto.State.Should().Be("SUBMITTED");
        receiverDto.State.Should().Be("SUBMITTED");

        var cancelResponse = await senderAdmin.CancelTransactionAsync(createResponse.Data!.Id);
        cancelResponse.IsSuccess.Should().BeTrue();
    }

    private async Task SetAddressDescriptionAsync(string address, string description)
    {
        var scopeFactory = _fixture.GetRequiredService<IServiceScopeFactory>();
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FireblocksDbContext>();
        var entity = await db.Addresses.SingleAsync(item => item.AddressValue == address);
        entity.Description = description;
        await db.SaveChangesAsync();
    }

    private static HubConnection BuildHubConnection(TestWebApplicationFactory factory, string? workspaceId = null)
    {
        var hubPath = string.IsNullOrWhiteSpace(workspaceId)
            ? "/hubs/admin"
            : $"/hubs/admin?workspaceId={Uri.EscapeDataString(workspaceId)}";
        var hubUrl = new Uri(factory.Server.BaseAddress!, hubPath);

        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }
}
