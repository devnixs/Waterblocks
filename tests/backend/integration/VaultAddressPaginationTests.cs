using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Models;
using Waterblocks.IntegrationTests.Infrastructure;
using Xunit;

namespace Waterblocks.IntegrationTests;

/// <summary>
/// Integration tests for the Fireblocks-compatible <c>addresses_paginated</c> endpoint.
///
/// Regression coverage for the infinite-pagination bug: a BTC wallet's primary
/// "Permanent" address is created with a NULL BIP44 index, which sorts last but was
/// emitted as cursor "0". A client walking pages with the <c>after</c> cursor would
/// then reset to the start of the list forever, hammering the endpoint many times a
/// second. The walk below mirrors the production client and must terminate via an
/// empty cursor while returning every address exactly once.
/// </summary>
public class VaultAddressPaginationTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Paginating_Addresses_With_NullIndex_Permanent_Address_Terminates_And_Returns_Each_Address_Once()
    {
        // Arrange: a BTC vault wallet. Creating the wallet seeds a single "Permanent"
        // address with a NULL BIP44 index (via EnsurePrimaryAddressAsync).
        const string assetId = "BTC";
        const int pageSize = 50;     // matches the production client's `count`
        // The bug only surfaces when the null-index "Permanent" address (which always
        // sorts last) is the final element of a *full* page, i.e. when the total address
        // count is an exact multiple of the page size. This mirrors the production vault:
        // 99 deposit addresses + 1 permanent = 100 = 2 x 50.
        const int depositCount = (pageSize * 2) - 1;
        var totalAddresses = depositCount + 1; // deposits + the permanent address

        var vault = await _fixture.FireblocksClient.CreateVaultAccountAsync(
            new CreateVaultAccountRequest { Name = "PaginationVault" });
        vault.Should().NotBeNull();
        var vaultId = vault!.Id;

        var createWallet = await _fixture.FireblocksClient.CreateWalletAsync(vaultId, assetId);
        createWallet.Should().NotBeNull();

        await SeedDepositAddressesAsync(vaultId, assetId, depositCount);

        // Act: walk the paginated endpoint exactly like the production client does,
        // following the `after` cursor until it is empty. A hard iteration cap stops
        // the test from hanging if the infinite-loop regression returns.
        var collected = new List<FireblocksPaginatedAddressDto>();
        const int maxIterations = 10; // the happy path needs 2; anything more signals a loop
        string? after = null;
        var iterations = 0;
        var terminatedNaturally = false;

        while (iterations < maxIterations)
        {
            iterations++;
            var page = await _fixture.FireblocksClient.GetAddressesPaginatedAsync(vaultId, assetId, pageSize, after);
            page.Should().NotBeNull();
            collected.AddRange(page!.Addresses);

            if (string.IsNullOrEmpty(page.Paging.After))
            {
                terminatedNaturally = true;
                break;
            }

            after = page.Paging.After;
        }

        // Assert: the walk ended because the server reported no more pages, not because
        // we hit the safety cap.
        terminatedNaturally.Should().BeTrue(
            "pagination must terminate via an empty `after` cursor; hitting the iteration " +
            "cap indicates the non-advancing-cursor infinite loop has regressed");

        // Every address is returned exactly once across all pages.
        collected.Should().HaveCount(totalAddresses);
        collected.Select(a => a.Address).Should().OnlyHaveUniqueItems(
            "a correct cursor never replays addresses from an earlier page");
        collected.Count(a => a.Type == "Permanent").Should().Be(1,
            "the null-index permanent address must appear exactly once, not loop the walk");
    }

    private async Task SeedDepositAddressesAsync(string vaultAccountId, string assetId, int depositCount)
    {
        using var db = _fixture.GetDbContext();

        var wallet = await db.Wallets
            .Include(w => w.Addresses)
            .FirstAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId);

        for (var index = 1; index <= depositCount; index++)
        {
            db.Addresses.Add(new Address
            {
                AddressValue = $"bc1qtestdeposit{index:D4}",
                Type = "DEPOSIT",
                AddressFormat = "SEGWIT",
                Bip44AddressIndex = index,
                WalletId = wallet.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }
}
