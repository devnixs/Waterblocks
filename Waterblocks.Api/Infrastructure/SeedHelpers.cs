using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Text.RegularExpressions;

namespace Waterblocks.Api.Infrastructure;

internal static class SeedHelpers
{
    private static readonly Regex SeedWorkspaceNameRegex = new(
        "^SEED_WORKSPACE_(\\d+)_NAME$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

        var workspace = db.Workspaces.FirstOrDefault(w => !w.IsDeleted);
        if (workspace == null)
        {
            workspace = new Workspace
            {
                Id = defaultWorkspaceId,
                Name = defaultWorkspaceName,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
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
                UpdatedAt = DateTimeOffset.UtcNow,
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

        SeedWorkspaceFromEnvironment(db, logger);

        logger.LogInformation("Seeded default workspace {WorkspaceId}", workspace.Id);
    }

    private static void SeedWorkspaceFromEnvironment(FireblocksDbContext db, ILogger logger)
    {
        var seedEntries = ReadSeedWorkspaceEntries(logger);
        if (seedEntries.Count == 0)
        {
            return;
        }

        var workspacesByName = db.Workspaces
            .Where(w => !w.IsDeleted)
            .ToDictionary(w => w.Name, StringComparer.Ordinal);

        var apiKeysByValue = db.ApiKeys
            .ToDictionary(k => k.Key, StringComparer.Ordinal);

        foreach (var entry in seedEntries)
        {
            if (!workspacesByName.TryGetValue(entry.Name, out var workspace))
            {
                workspace = new Workspace
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = entry.Name,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                };
                db.Workspaces.Add(workspace);
                workspacesByName[workspace.Name] = workspace;
                logger.LogInformation(
                    "Created seeded workspace {WorkspaceName} from environment index {SeedIndex}",
                    workspace.Name, entry.Index);
            }

            if (apiKeysByValue.TryGetValue(entry.ApiKey, out var existingApiKey))
            {
                if (existingApiKey.WorkspaceId == workspace.Id)
                {
                    logger.LogInformation(
                        "Skipping seeded API key for workspace {WorkspaceName} from environment index {SeedIndex}: key already exists",
                        workspace.Name, entry.Index);
                }
                else
                {
                    logger.LogWarning(
                        "Skipping seeded API key for workspace {WorkspaceName} from environment index {SeedIndex}: key already assigned to another workspace",
                        workspace.Name, entry.Index);
                }

                continue;
            }

            var apiKey = new ApiKey
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Seeded",
                Key = entry.ApiKey,
                WorkspaceId = workspace.Id,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            db.ApiKeys.Add(apiKey);
            apiKeysByValue[apiKey.Key] = apiKey;

            logger.LogInformation(
                "Seeded API key for workspace {WorkspaceName} from environment index {SeedIndex}",
                workspace.Name, entry.Index);
        }

        db.SaveChanges();
    }

    private static List<SeedWorkspaceEntry> ReadSeedWorkspaceEntries(ILogger logger)
    {
        var entries = new Dictionary<int, (string? Name, string? ApiKey)>();
        var environmentVariables = Environment.GetEnvironmentVariables();

        foreach (DictionaryEntry variable in environmentVariables)
        {
            var key = variable.Key?.ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var match = SeedWorkspaceNameRegex.Match(key);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var index))
            {
                continue;
            }

            var name = variable.Value?.ToString()?.Trim();
            var apiKey = Environment.GetEnvironmentVariable($"SEED_WORKSPACE_{index}_APIKEY")?.Trim();

            entries[index] = (name, apiKey);
        }

        foreach (var entry in entries.Where(e => string.IsNullOrWhiteSpace(e.Value.Name) || string.IsNullOrWhiteSpace(e.Value.ApiKey)))
        {
            logger.LogWarning(
                "Skipping seed workspace environment index {SeedIndex}: both NAME and APIKEY seed variables are required",
                entry.Key);
        }

        return entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Value.Name) && !string.IsNullOrWhiteSpace(e.Value.ApiKey))
            .OrderBy(e => e.Key)
            .Select(e => new SeedWorkspaceEntry(e.Key, e.Value.Name!, e.Value.ApiKey!))
            .ToList();
    }

    private sealed record SeedWorkspaceEntry(int Index, string Name, string ApiKey);
}
