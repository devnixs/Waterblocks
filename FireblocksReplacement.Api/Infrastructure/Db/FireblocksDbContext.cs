using Microsoft.EntityFrameworkCore;
using FireblocksReplacement.Api.Models;

namespace FireblocksReplacement.Api.Infrastructure.Db;

public class FireblocksDbContext : DbContext
{
    public FireblocksDbContext(DbContextOptions<FireblocksDbContext> options)
        : base(options)
    {
    }

    public DbSet<VaultAccount> VaultAccounts { get; set; }
    public DbSet<Wallet> Wallets { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<Asset> Assets { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Workspace> Workspaces { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<AdminSetting> AdminSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.ApiKeys)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure VaultAccount
        modelBuilder.Entity<VaultAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CustomerRefId);
            entity.HasMany(e => e.Wallets)
                .WithOne(w => w.VaultAccount)
                .HasForeignKey(w => w.VaultAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.VaultAccounts)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Wallet
        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.VaultAccountId, e.AssetId }).IsUnique();
            entity.HasMany(e => e.Addresses)
                .WithOne(a => a.Wallet)
                .HasForeignKey(a => a.WalletId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Address
        modelBuilder.Entity<Address>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AddressValue);
        });

        // Configure Asset
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasKey(e => e.AssetId);
        });

        // Configure Transaction
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalTxId)
                .IsUnique()
                .HasFilter("\"ExternalTxId\" IS NOT NULL");
            entity.HasIndex(e => e.State);
            entity.HasIndex(e => e.Hash);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.WorkspaceId);
            entity.Property(e => e.State).HasConversion<string>();
            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Transactions)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
        });

        // Seed some common assets
        modelBuilder.Entity<Asset>().HasData(
            new Asset
            {
                AssetId = "BTC",
                Name = "Bitcoin",
                Symbol = "BTC",
                Decimals = 8,
                Type = "BASE_ASSET",
                NativeAsset = "BTC",
                IsActive = true
            },
            new Asset
            {
                AssetId = "ETH",
                Name = "Ethereum",
                Symbol = "ETH",
                Decimals = 18,
                Type = "BASE_ASSET",
                NativeAsset = "ETH",
                IsActive = true
            },
            new Asset
            {
                AssetId = "USDT",
                Name = "Tether",
                Symbol = "USDT",
                Decimals = 6,
                Type = "ERC20",
                NativeAsset = "ETH",
                IsActive = true
            },
            new Asset
            {
                AssetId = "USDC",
                Name = "USD Coin",
                Symbol = "USDC",
                Decimals = 6,
                Type = "ERC20",
                NativeAsset = "ETH",
                IsActive = true
            }
        );
    }
}
