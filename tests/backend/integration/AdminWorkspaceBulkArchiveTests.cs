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

public class AdminWorkspaceBulkArchiveTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AdminWorkspaceBulkArchiveTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ArchiveAllWorkspaces_ArchivesEveryEligibleWorkspaceAndPreservesDefault()
    {
        using var factory = _fixture.CreateFactory(new Dictionary<string, string?>
        {
            ["ARCHIVE_ALL_WORKSPACES_ENABLED"] = "true",
        });
        var client = new AdminApiClient(factory.CreateClient());

        var existingWorkspaces = await client.GetWorkspacesAsync();
        var defaultWorkspace = existingWorkspaces.Data?.SingleOrDefault(workspace => workspace.Name == "Default");
        var alphaWorkspace = await client.CreateWorkspaceAsync($"Alpha-{Guid.NewGuid():N}"[..16]);
        var betaWorkspace = await client.CreateWorkspaceAsync($"Beta-{Guid.NewGuid():N}"[..15]);
        var archivedWorkspace = await client.CreateWorkspaceAsync($"Archived-{Guid.NewGuid():N}"[..18]);

        Assert.NotNull(defaultWorkspace);
        Assert.NotNull(alphaWorkspace.Data);
        Assert.NotNull(betaWorkspace.Data);
        Assert.NotNull(archivedWorkspace.Data);

        var deleteResponse = await client.DeleteWorkspaceAsync(archivedWorkspace.Data!.Id);
        Assert.True(deleteResponse.Data);

        DateTimeOffset? deletedAtBefore;
        DateTimeOffset updatedAtBefore;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FireblocksDbContext>();
            var archivedBeforeBulk = await db.Workspaces
                .SingleAsync(w => w.Id == archivedWorkspace.Data.Id);

            deletedAtBefore = archivedBeforeBulk.DeletedAt;
            updatedAtBefore = archivedBeforeBulk.UpdatedAt;
        }

        var response = await client.ArchiveAllWorkspacesAsync();

        Assert.True(response.Data);
        Assert.Null(response.Error);

        var workspacesResponse = await client.GetWorkspacesAsync();
        Assert.NotNull(workspacesResponse.Data);
        Assert.Contains(workspacesResponse.Data!, workspace => workspace.Id == defaultWorkspace!.Id);
        Assert.DoesNotContain(workspacesResponse.Data!, workspace => workspace.Id == alphaWorkspace.Data!.Id);
        Assert.DoesNotContain(workspacesResponse.Data!, workspace => workspace.Id == betaWorkspace.Data!.Id);
        Assert.DoesNotContain(workspacesResponse.Data!, workspace => workspace.Id == archivedWorkspace.Data!.Id);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FireblocksDbContext>();
            var storedWorkspaces = await db.Workspaces
                .Where(w => w.Id == defaultWorkspace!.Id
                    || w.Id == alphaWorkspace.Data!.Id
                    || w.Id == betaWorkspace.Data!.Id
                    || w.Id == archivedWorkspace.Data!.Id)
                .ToDictionaryAsync(w => w.Id);

            var storedDefault = storedWorkspaces[defaultWorkspace.Id];
            Assert.False(storedDefault.IsDeleted);
            Assert.Null(storedDefault.DeletedAt);

            var storedAlpha = storedWorkspaces[alphaWorkspace.Data!.Id];
            Assert.True(storedAlpha.IsDeleted);
            Assert.NotNull(storedAlpha.DeletedAt);
            Assert.Equal(storedAlpha.DeletedAt, storedAlpha.UpdatedAt);

            var storedBeta = storedWorkspaces[betaWorkspace.Data!.Id];
            Assert.True(storedBeta.IsDeleted);
            Assert.NotNull(storedBeta.DeletedAt);
            Assert.Equal(storedBeta.DeletedAt, storedBeta.UpdatedAt);

            var storedArchived = storedWorkspaces[archivedWorkspace.Data!.Id];
            Assert.True(storedArchived.IsDeleted);
            Assert.Equal(deletedAtBefore, storedArchived.DeletedAt);
            Assert.Equal(updatedAtBefore, storedArchived.UpdatedAt);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task ArchiveAllWorkspaces_ReturnsFeatureDisabled_WhenFlagIsMissingOrFalse(string? featureFlagValue)
    {
        using var factory = _fixture.CreateFactory(
            featureFlagValue is null
                ? null
                : new Dictionary<string, string?> { ["ARCHIVE_ALL_WORKSPACES_ENABLED"] = featureFlagValue });
        var client = new AdminApiClient(factory.CreateClient());

        var defaultWorkspace = await client.CreateWorkspaceAsync($"FlagOff-{Guid.NewGuid():N}"[..16]);
        var alphaWorkspace = await client.CreateWorkspaceAsync($"Alpha-{Guid.NewGuid():N}"[..16]);

        Assert.NotNull(defaultWorkspace.Data);
        Assert.NotNull(alphaWorkspace.Data);

        var response = await client.ArchiveAllWorkspacesAsync();

        Assert.NotNull(response.Error);
        Assert.Equal("FEATURE_DISABLED", response.Error!.Code);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FireblocksDbContext>();
        var storedWorkspaces = await db.Workspaces
            .Where(w => w.Id == defaultWorkspace.Data!.Id || w.Id == alphaWorkspace.Data!.Id)
            .ToDictionaryAsync(w => w.Id);

        Assert.False(storedWorkspaces[defaultWorkspace.Data!.Id].IsDeleted);
        Assert.False(storedWorkspaces[alphaWorkspace.Data!.Id].IsDeleted);
    }

    [Fact]
    public async Task WorkspaceRealtime_EmitsAdminWideEvent_WhenVisibleWorkspaceListChanges()
    {
        using var factory = _fixture.CreateFactory(new Dictionary<string, string?>
        {
            ["ARCHIVE_ALL_WORKSPACES_ENABLED"] = "true",
        });
        var client = new AdminApiClient(factory.CreateClient());

        var receivedEvents = new ConcurrentQueue<DateTimeOffset>();
        var firstEvent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEvent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdEvent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = BuildHubConnection(factory, workspaceId: null);
        connection.On("workspacesUpdated", () =>
        {
            receivedEvents.Enqueue(DateTimeOffset.UtcNow);
            if (receivedEvents.Count == 1)
            {
                firstEvent.TrySetResult();
            }
            else if (receivedEvents.Count == 2)
            {
                secondEvent.TrySetResult();
            }
            else
            {
                thirdEvent.TrySetResult();
            }
        });

        await connection.StartAsync();

        var createdWorkspace = await client.CreateWorkspaceAsync($"Realtime-{Guid.NewGuid():N}"[..18]);
        createdWorkspace.IsSuccess.Should().BeTrue();
        await firstEvent.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var archivedWorkspace = await client.CreateWorkspaceAsync($"Archive-{Guid.NewGuid():N}"[..17]);
        archivedWorkspace.IsSuccess.Should().BeTrue();
        await secondEvent.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var archiveResponse = await client.ArchiveAllWorkspacesAsync();
        archiveResponse.IsSuccess.Should().BeTrue();
        await thirdEvent.Task.WaitAsync(TimeSpan.FromSeconds(10));

        receivedEvents.Count.Should().BeGreaterOrEqualTo(3);
    }

    private static HubConnection BuildHubConnection(TestWebApplicationFactory factory, string? workspaceId)
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
