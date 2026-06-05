using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Waterblocks.Api.Infrastructure.Db;
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
        summaryResponse.Data!.Count.Should().Be(2);
        summaryResponse.Data.Items.Should().HaveCount(2);
        summaryResponse.Data.Items.Should().OnlyContain(item => item.State != "COMPLETED");
        summaryResponse.Data.Items.Select(item => item.Id).Should().Contain(new[]
        {
            pendingIncomingResponse.Data!.Id,
            crossWorkspaceResponse.Data!.Id,
        });

        summaryResponse.Data.Items[0].Id.Should().Be(crossWorkspaceResponse.Data!.Id, "items should be ordered newest first");
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

    private async Task SetAddressDescriptionAsync(string address, string description)
    {
        var scopeFactory = _fixture.GetRequiredService<IServiceScopeFactory>();
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FireblocksDbContext>();
        var entity = await db.Addresses.SingleAsync(item => item.AddressValue == address);
        entity.Description = description;
        await db.SaveChangesAsync();
    }

    private static HubConnection BuildHubConnection(TestWebApplicationFactory factory)
    {
        var hubUrl = new Uri(factory.Server.BaseAddress!, "/hubs/admin");

        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }
}
