using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Microsoft.Extensions.Logging;

namespace Waterblocks.Api.Infrastructure;

internal static class SeedHelpers
{
    internal static string DeriveSymbol(string assetId)
    {
        var idx = assetId.IndexOf('_');
        if (idx <= 0)
        {
            return assetId.Length > 10 ? assetId[..10] : assetId;
        }

        var symbol = assetId[..idx];
        return symbol.Length > 10 ? symbol[..10] : symbol;
    }

    internal static void SeedWorkspaces(FireblocksDbContext db, ILogger logger)
    {
        const string defaultWorkspaceId = "00000000-0000-0000-0000-000000000001";
        const string defaultWorkspaceName = "Default";
        const string defaultApiKey = "admin";

        var workspace = db.Workspaces.FirstOrDefault();
        if (workspace == null)
        {
            workspace = new Workspace
            {
                Id = defaultWorkspaceId,
                Name = defaultWorkspaceName,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Workspaces.Add(workspace);
            db.SaveChanges();
        }

        if (!db.ApiKeys.Any(k => k.WorkspaceId == workspace.Id))
        {
            var apiKey = new ApiKey
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Default",
                Key = defaultApiKey,
                WorkspaceId = workspace.Id,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.ApiKeys.Add(apiKey);
            db.SaveChanges();
        }

        if (db.VaultAccounts.Any(v => string.IsNullOrEmpty(v.WorkspaceId)))
        {
            foreach (var vault in db.VaultAccounts.Where(v => string.IsNullOrEmpty(v.WorkspaceId)))
            {
                vault.WorkspaceId = workspace.Id;
                vault.UpdatedAt = DateTimeOffset.UtcNow;
            }
            db.SaveChanges();
        }

        if (db.Transactions.Any(t => string.IsNullOrEmpty(t.WorkspaceId)))
        {
            foreach (var tx in db.Transactions.Where(t => string.IsNullOrEmpty(t.WorkspaceId)))
            {
                tx.WorkspaceId = workspace.Id;
                tx.UpdatedAt = DateTimeOffset.UtcNow;
            }
            db.SaveChanges();
        }

        logger.LogInformation("Seeded default workspace {WorkspaceId}", workspace.Id);
    }
}
