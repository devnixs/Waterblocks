using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Dtos.Fireblocks;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Waterblocks.Api.Utils;

namespace Waterblocks.Api.Services;

public interface IExchangeAccountService
{
    Task<AddExchangeAccountResponseDto> CreateAsync(AddExchangeAccountRequestDto request);
    Task<List<ExchangeAccountDto>> ListAsync();
    Task<PagedExchangeAccountsResponseDto> ListPagedAsync(int limit, string? before, string? after);
    Task<ExchangeAccountDto> GetAsync(string exchangeAccountId);
    Task<ExchangeAssetDto> GetAssetAsync(string exchangeAccountId, string assetId);
    Task<ExchangeAccountDto> GetCollateralAccountAsync(string mainExchangeAccountId);
    ExchangeCredentialsPublicKeyDto GetCredentialsPublicKey();

    Task<List<AdminExchangeAccountDto>> ListForAdminAsync();
    Task<AdminExchangeAccountDto> GetForAdminAsync(string exchangeAccountId);
    Task<AdminExchangeAccountDto> CreateConnectedAsync(CreateAdminExchangeAccountRequestDto request);
    Task<AdminExchangeAccountDto> ConnectAsync(string exchangeAccountId, ConnectAdminExchangeAccountRequestDto request);
    Task<AdminExchangeAssetDto> UpdateAssetAsync(string exchangeAccountId, string assetId, UpdateAdminExchangeAssetRequestDto request);
    Task<List<AdminCollateralTransferDto>> ListTransfersAsync(string exchangeAccountId);
    Task<AdminCollateralTransferDto> MapTransferAsync(Transaction transaction);
}

public sealed class ExchangeAccountService : IExchangeAccountService
{
    private const int MinPageSize = 1;
    private const int MaxPageSize = 5;
    private const int MaxNameLength = 255;
    private const int MaxTransfers = 200;
    private const int MaskedKeyEdgeLength = 4;

    private const string TestCredentialsPublicKey =
        "-----BEGIN PUBLIC KEY-----\n" +
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA6TQCcjybyXBrKAuUWPUM\n" +
        "xaaGZyuRw+aJR60ORMm64ZHc5fqBTLxUJx6g1GxgslXvJCIWFZTvDm1UKkni9lkY\n" +
        "QoH9V0kW0e/bpDFlNi8zesk/zYfof7fyTR585qB5TiFMPHkQLORc7j28A9jKQRHF\n" +
        "Jl/2hEYL7ccsMmzer7LHsaYM8FXWs7BF914XhRLpEa+BmkVFacaF707XZ44Sx+Uz\n" +
        "ZSkFe3KgHCUosakfpUucY48MXgFxqOpyR5tF0XAWYOORL2L4VyyqX/Bb+uhyZnaK\n" +
        "4Nnye8U8SaKGPUK+VkByv4GeHAQhsFjfZ7paXyK3Hhe55ZuPYGFMWANnJMTr/Dl6\n" +
        "UwIDAQAB\n" +
        "-----END PUBLIC KEY-----\n";

    private readonly FireblocksDbContext _context;
    private readonly WorkspaceContext _workspace;
    private readonly IGateSimulatorClient _gateSimulator;
    private readonly IExchangeCollateralLedger _collateralLedger;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly IdempotencyContext _idempotency;

    public ExchangeAccountService(
        FireblocksDbContext context,
        WorkspaceContext workspace,
        IGateSimulatorClient gateSimulator,
        IExchangeCollateralLedger collateralLedger,
        IRealtimeNotifier realtimeNotifier,
        IdempotencyContext idempotency)
    {
        _context = context;
        _workspace = workspace;
        _gateSimulator = gateSimulator;
        _collateralLedger = collateralLedger;
        _realtimeNotifier = realtimeNotifier;
        _idempotency = idempotency;
    }

    private string WorkspaceId => string.IsNullOrEmpty(_workspace.WorkspaceId)
        ? throw new UnauthorizedAccessException("Workspace is required")
        : _workspace.WorkspaceId;

    private IQueryable<ExchangeAccount> WorkspaceAccounts => _context.ExchangeAccounts
        .AsNoTracking()
        .Include(account => account.Assets)
        .Where(account => account.WorkspaceId == WorkspaceId);

    public async Task<AddExchangeAccountResponseDto> CreateAsync(AddExchangeAccountRequestDto request)
    {
        var exchangeType = RequireExchangeType(request.ExchangeType);
        if (exchangeType == ExchangeTypes.Gate)
        {
            await RequireNoGateAccountAsync();
        }

        var mainAccountId = string.IsNullOrWhiteSpace(request.MainAccountId) ? null : request.MainAccountId.Trim();
        if (mainAccountId?.Length > MaxNameLength)
        {
            throw ExchangeOperationException.BadRequest(
                "MAIN_ACCOUNT_ID_TOO_LONG",
                $"mainAccountId must have at most {MaxNameLength} characters");
        }

        var account = new ExchangeAccount
        {
            WorkspaceId = WorkspaceId,
            Name = RequireName(request.Name),
            ExchangeType = exchangeType,
            MainAccountId = mainAccountId,
        };
        var response = new AddExchangeAccountResponseDto
        {
            Id = account.Id,
            Name = account.Name,
            ExchangeType = account.ExchangeType,
        };

        _context.ExchangeAccounts.Add(account);
        _idempotency.StageReply(_context, StatusCodes.Status201Created, response);
        await SaveAccountAsync();
        _idempotency.MarkReplyStored();
        await _realtimeNotifier.NotifyVaultsUpdatedAsync(account.WorkspaceId);

        return response;
    }

    public async Task<List<ExchangeAccountDto>> ListAsync()
    {
        var views = await BuildViewsAsync(await LoadWorkspaceAccountsAsync());
        return views.Select(ToFireblocksAccount).ToList();
    }

    public async Task<PagedExchangeAccountsResponseDto> ListPagedAsync(int limit, string? before, string? after)
    {
        if (limit < MinPageSize || limit > MaxPageSize)
        {
            throw ExchangeOperationException.BadRequest(
                "INVALID_LIMIT",
                $"limit must be between {MinPageSize} and {MaxPageSize}");
        }

        var page = await LoadPageAsync(limit, before, after);
        var views = await BuildViewsAsync(page);
        var firstId = page.FirstOrDefault()?.Id;
        var lastId = page.LastOrDefault()?.Id;
        var accounts = _context.ExchangeAccounts.AsNoTracking().Where(account => account.WorkspaceId == WorkspaceId);

        return new PagedExchangeAccountsResponseDto
        {
            Exchanges = views.Select(ToFireblocksAccount).ToList(),
            Paging = new ExchangeAccountsPagingDto
            {
                Before = firstId != null && await accounts.AnyAsync(account => string.Compare(account.Id, firstId) < 0)
                    ? firstId
                    : null,
                After = lastId != null && await accounts.AnyAsync(account => string.Compare(account.Id, lastId) > 0)
                    ? lastId
                    : null,
            },
        };
    }

    public async Task<ExchangeAccountDto> GetAsync(string exchangeAccountId)
    {
        return ToFireblocksAccount(await LoadViewAsync(exchangeAccountId));
    }

    public async Task<ExchangeAssetDto> GetAssetAsync(string exchangeAccountId, string assetId)
    {
        var account = await RequireAccountAsync(exchangeAccountId);
        await RequireKnownAssetAsync(assetId);
        var snapshot = await ReadSimulatorAsync(account);
        var row = account.Assets.FirstOrDefault(asset => asset.AssetId == assetId);
        return ToFireblocksAsset(BuildAssetView(account, snapshot, row, assetId));
    }

    public async Task<ExchangeAccountDto> GetCollateralAccountAsync(string mainExchangeAccountId)
    {
        var view = await LoadViewAsync(mainExchangeAccountId);
        var collateralAccount = ToFireblocksAccount(view);
        collateralAccount.Id = view.Account.CollateralAccountId;
        collateralAccount.MainAccountId = view.Account.Id;
        collateralAccount.IsSubaccount = true;
        return collateralAccount;
    }

    public ExchangeCredentialsPublicKeyDto GetCredentialsPublicKey()
    {
        return new ExchangeCredentialsPublicKeyDto
        {
            PublicKey = TestCredentialsPublicKey,
            TenantId = WorkspaceId,
        };
    }

    public async Task<List<AdminExchangeAccountDto>> ListForAdminAsync()
    {
        var views = await BuildViewsAsync(await LoadWorkspaceAccountsAsync());
        return views.Select(ToAdminAccount).ToList();
    }

    public async Task<AdminExchangeAccountDto> GetForAdminAsync(string exchangeAccountId)
    {
        return ToAdminAccount(await LoadViewAsync(exchangeAccountId));
    }

    public async Task<AdminExchangeAccountDto> CreateConnectedAsync(CreateAdminExchangeAccountRequestDto request)
    {
        var exchangeType = RequireExchangeType(request.ExchangeType);
        RequireGateAccountType(exchangeType);
        var name = RequireName(request.Name);
        await RequireNoGateAccountAsync();
        var connection = await VerifyConnectionAsync(request.ApiKey, request.ApiSecret);

        var account = new ExchangeAccount
        {
            WorkspaceId = WorkspaceId,
            Name = name,
            ExchangeType = exchangeType,
        };
        ApplyConnection(account, connection);
        _context.ExchangeAccounts.Add(account);
        await SaveAccountAsync();
        await _realtimeNotifier.NotifyVaultsUpdatedAsync(account.WorkspaceId);

        return await GetForAdminAsync(account.Id);
    }

    public async Task<AdminExchangeAccountDto> ConnectAsync(
        string exchangeAccountId,
        ConnectAdminExchangeAccountRequestDto request)
    {
        var account = await _context.ExchangeAccounts
            .FirstOrDefaultAsync(item => item.Id == exchangeAccountId && item.WorkspaceId == WorkspaceId)
            ?? throw AccountNotFound(exchangeAccountId);

        RequireGateAccountType(account.ExchangeType);
        if (account.IsConnected)
        {
            throw AlreadyConnected("This exchange account already has a connection");
        }

        var connection = await VerifyConnectionAsync(request.ApiKey, request.ApiSecret);
        ApplyConnection(account, connection);
        await SaveAccountAsync();
        await _realtimeNotifier.NotifyVaultsUpdatedAsync(account.WorkspaceId);

        return await GetForAdminAsync(account.Id);
    }

    public async Task<AdminExchangeAssetDto> UpdateAssetAsync(
        string exchangeAccountId,
        string assetId,
        UpdateAdminExchangeAssetRequestDto request)
    {
        var account = await RequireAccountAsync(exchangeAccountId);
        await RequireKnownAssetAsync(assetId);

        var exchangeHold = ParseOptionalAmount(request.ExchangeHold, "exchangeHold", "INVALID_EXCHANGE_HOLD");
        var credit = ParseOptionalAmount(request.Credit, "credit", "INVALID_CREDIT");
        if (exchangeHold == null && credit == null)
        {
            throw ExchangeOperationException.BadRequest("NOTHING_TO_UPDATE", "Send exchangeHold or credit");
        }

        if (credit != null && account.IsConnected)
        {
            throw ExchangeOperationException.BadRequest(
                "CREDIT_READ_ONLY",
                "The credit of a connected account comes from the Gate simulator");
        }

        await _collateralLedger.EnsureAssetRowAsync(account.Id, assetId);
        var row = _context.ExchangeAccountAssets
            .Where(asset => asset.ExchangeAccountId == account.Id && asset.AssetId == assetId);
        var now = DateTimeOffset.UtcNow;

        if (exchangeHold != null)
        {
            var value = exchangeHold.Value;
            await row.ExecuteUpdateAsync(update => update
                .SetProperty(asset => asset.ExchangeHold, value)
                .SetProperty(asset => asset.UpdatedAt, now));
        }

        if (credit != null)
        {
            var value = credit.Value;
            await row.ExecuteUpdateAsync(update => update
                .SetProperty(asset => asset.Credit, value)
                .SetProperty(asset => asset.UpdatedAt, now));
        }

        await _realtimeNotifier.NotifyVaultsUpdatedAsync(account.WorkspaceId);

        var view = await LoadViewAsync(exchangeAccountId);
        return ToAdminAsset(view.Assets.First(asset => asset.AssetId == assetId));
    }

    public async Task<List<AdminCollateralTransferDto>> ListTransfersAsync(string exchangeAccountId)
    {
        var account = await RequireAccountAsync(exchangeAccountId);
        var transactions = await _context.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.ExchangeAccountId == account.Id)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Take(MaxTransfers)
            .ToListAsync();

        return await MapTransfersAsync(transactions);
    }

    public async Task<AdminCollateralTransferDto> MapTransferAsync(Transaction transaction)
    {
        return (await MapTransfersAsync(new[] { transaction })).Single();
    }

    private async Task<List<AdminCollateralTransferDto>> MapTransfersAsync(IReadOnlyCollection<Transaction> transactions)
    {
        var vaultIds = transactions.Select(transaction => transaction.VaultAccountId).Distinct().ToList();
        var vaultNames = await _context.VaultAccounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(vault => vaultIds.Contains(vault.Id))
            .ToDictionaryAsync(vault => vault.Id, vault => vault.Name);

        var transactionIds = transactions.Select(transaction => transaction.Id).ToList();
        var mirrorEvents = await _context.ExchangeMirrorEvents
            .AsNoTracking()
            .Where(item => transactionIds.Contains(item.TransactionId))
            .ToListAsync();
        var latestEvents = mirrorEvents
            .GroupBy(item => item.TransactionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.CreatedAt).Last());

        return transactions
            .Select(transaction => ToTransferDto(
                transaction,
                vaultNames.GetValueOrDefault(transaction.VaultAccountId, transaction.VaultAccountId),
                latestEvents.GetValueOrDefault(transaction.Id)))
            .ToList();
    }

    private static AdminCollateralTransferDto ToTransferDto(
        Transaction transaction,
        string vaultName,
        ExchangeMirrorEvent? mirrorEvent)
    {
        var collateralName = $"{transaction.ExchangeAccountName} (Collateral)";
        var isAdd = transaction.CollateralDirection == CollateralDirection.ADD;
        return new AdminCollateralTransferDto
        {
            Id = TransactionCompositeId.Build(transaction.WorkspaceId, transaction.Id),
            Direction = transaction.CollateralDirection?.ToString() ?? string.Empty,
            AssetId = transaction.AssetId,
            Amount = AmountText.Format(transaction.Amount),
            RequestedAmount = AmountText.Format(transaction.RequestedAmount),
            NetworkFee = AmountText.Format(transaction.NetworkFee),
            TreatAsGrossAmount = transaction.TreatAsGrossAmount,
            VaultAccountId = transaction.VaultAccountId,
            FromName = isAdd ? vaultName : collateralName,
            ToName = isAdd ? collateralName : vaultName,
            Note = transaction.Note,
            State = transaction.State.ToString(),
            SubStatus = transaction.SubStatus,
            FailureReason = transaction.FailureReason,
            MirrorState = mirrorEvent?.Status.ToString(),
            MirrorError = mirrorEvent?.LastError,
            InitiatedBy = transaction.InitiatedBy,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt,
        };
    }

    private Task<List<ExchangeAccount>> LoadWorkspaceAccountsAsync()
    {
        return WorkspaceAccounts
            .OrderBy(account => account.CreatedAt)
            .ThenBy(account => account.Id)
            .ToListAsync();
    }

    private async Task<List<ExchangeAccount>> LoadPageAsync(int limit, string? before, string? after)
    {
        if (!string.IsNullOrWhiteSpace(before))
        {
            var previous = await WorkspaceAccounts
                .Where(account => string.Compare(account.Id, before) < 0)
                .OrderByDescending(account => account.Id)
                .Take(limit)
                .ToListAsync();
            previous.Reverse();
            return previous;
        }

        var query = WorkspaceAccounts;
        if (!string.IsNullOrWhiteSpace(after))
        {
            query = query.Where(account => string.Compare(account.Id, after) > 0);
        }

        return await query.OrderBy(account => account.Id).Take(limit).ToListAsync();
    }

    private async Task<ExchangeAccount> RequireAccountAsync(string exchangeAccountId)
    {
        return await WorkspaceAccounts.FirstOrDefaultAsync(account => account.Id == exchangeAccountId)
            ?? throw AccountNotFound(exchangeAccountId);
    }

    private async Task RequireKnownAssetAsync(string assetId)
    {
        if (!await _context.Assets.AnyAsync(asset => asset.AssetId == assetId))
        {
            throw ExchangeOperationException.NotFound("ASSET_NOT_FOUND", $"Asset {assetId} not found");
        }
    }

    private async Task<AccountView> LoadViewAsync(string exchangeAccountId)
    {
        var account = await RequireAccountAsync(exchangeAccountId);
        return (await BuildViewsAsync(new[] { account })).Single();
    }

    private async Task<List<AccountView>> BuildViewsAsync(IReadOnlyList<ExchangeAccount> accounts)
    {
        var snapshots = await Task.WhenAll(accounts.Select(ReadSimulatorAsync));
        return accounts.Select((account, index) => BuildView(account, snapshots[index])).ToList();
    }

    private Task<GateSimulatorAccountInfo?> ReadSimulatorAsync(ExchangeAccount account)
    {
        return account.IsConnected
            ? _gateSimulator.TryGetAccountAsync(account.GateSimulatorAccount!)
            : Task.FromResult<GateSimulatorAccountInfo?>(null);
    }

    private static AccountView BuildView(ExchangeAccount account, GateSimulatorAccountInfo? snapshot)
    {
        var assetIds = account.Assets.Select(asset => asset.AssetId).ToHashSet(StringComparer.Ordinal);
        if (account.IsConnected)
        {
            assetIds.UnionWith(ExchangeTypes.GateSimulatorAssets);
        }

        var assets = assetIds
            .OrderBy(assetId => assetId, StringComparer.Ordinal)
            .Select(assetId => BuildAssetView(
                account,
                snapshot,
                account.Assets.FirstOrDefault(asset => asset.AssetId == assetId),
                assetId))
            .ToList();

        return new AccountView(account, !account.IsConnected || snapshot != null, assets);
    }

    private static AssetView BuildAssetView(
        ExchangeAccount account,
        GateSimulatorAccountInfo? snapshot,
        ExchangeAccountAsset? row,
        string assetId)
    {
        var custody = row?.Custody ?? 0m;
        var locked = row?.Locked ?? 0m;
        var exchangeHold = row?.ExchangeHold ?? 0m;
        var storedCredit = row?.Credit ?? 0m;
        var free = custody - locked - exchangeHold;

        var available = Math.Max(free, 0m);
        var credit = storedCredit;
        if (account.IsConnected && snapshot == null)
        {
            available = 0m;
        }
        else if (account.IsConnected)
        {
            var balance = snapshot!.Balances.GetValueOrDefault(assetId) ?? new GateSimulatorBalance(0m, 0m);
            available = Math.Max(Math.Min(free, balance.SpotAvailable), 0m);
            credit = balance.SpotAvailable + balance.SpotLocked;
        }

        return new AssetView(assetId, custody, locked, exchangeHold, available, credit, row?.CollateralAddress);
    }

    private static ExchangeAccountDto ToFireblocksAccount(AccountView view)
    {
        return new ExchangeAccountDto
        {
            Id = view.Account.Id,
            Type = view.Account.ExchangeType,
            Name = view.Account.Name,
            Status = view.Account.Status,
            Assets = view.Assets.Select(ToFireblocksAsset).ToList(),
            Success = view.Success,
            IsSubaccount = view.Account.MainAccountId != null,
            MainAccountId = view.Account.MainAccountId,
        };
    }

    private static ExchangeAssetDto ToFireblocksAsset(AssetView asset)
    {
        var total = AmountText.Format(asset.Custody);
        return new ExchangeAssetDto
        {
            Id = asset.AssetId,
            Balance = total,
            Total = total,
            LockedAmount = AmountText.Format(asset.Locked + asset.ExchangeHold),
            Available = AmountText.Format(asset.Available),
            Credit = AmountText.Format(asset.Credit),
            ProviderSymbol = asset.AssetId,
            AssetSymbol = asset.AssetId,
            AssetLegacyId = asset.AssetId,
        };
    }

    private static AdminExchangeAccountDto ToAdminAccount(AccountView view)
    {
        var account = view.Account;
        return new AdminExchangeAccountDto
        {
            Id = account.Id,
            CollateralAccountId = account.CollateralAccountId,
            Name = account.Name,
            ExchangeType = account.ExchangeType,
            Status = account.Status,
            IsSubaccount = account.MainAccountId != null,
            MainAccountId = account.MainAccountId,
            Success = view.Success,
            Connection = account.IsConnected
                ? new AdminExchangeConnectionDto
                {
                    SimulatorAccount = account.GateSimulatorAccount!,
                    SimulatorUserId = account.GateSimulatorUserId ?? 0,
                    MaskedApiKey = account.MaskedApiKey ?? string.Empty,
                    ConnectedAt = account.ConnectedAt,
                }
                : null,
            Assets = view.Assets.Select(ToAdminAsset).ToList(),
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt,
        };
    }

    private static AdminExchangeAssetDto ToAdminAsset(AssetView asset)
    {
        return new AdminExchangeAssetDto
        {
            AssetId = asset.AssetId,
            Total = AmountText.Format(asset.Custody),
            Available = AmountText.Format(asset.Available),
            Credit = AmountText.Format(asset.Credit),
            LockedAmount = AmountText.Format(asset.Locked + asset.ExchangeHold),
            Locked = AmountText.Format(asset.Locked),
            ExchangeHold = AmountText.Format(asset.ExchangeHold),
            CollateralAddress = asset.CollateralAddress,
        };
    }

    private async Task<GateConnection> VerifyConnectionAsync(string? apiKey, string? apiSecret)
    {
        var key = apiKey?.Trim();
        var secret = apiSecret?.Trim();
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(secret))
        {
            throw ExchangeOperationException.BadRequest("API_CREDENTIALS_REQUIRED", "API key and API secret are required");
        }

        var userId = await _gateSimulator.VerifyCredentialsAsync(key, secret);
        var simulatorAccount = await _gateSimulator.FindAccountByUserIdAsync(userId)
            ?? throw ExchangeOperationException.BadRequest(
                "GATE_SIMULATOR_ACCOUNT_NOT_FOUND",
                "The Gate simulator has no account for this API key");

        var alreadyConnected = await _context.ExchangeAccounts
            .AnyAsync(account => account.GateSimulatorUserId == userId);
        if (alreadyConnected)
        {
            throw SimulatorAccountInUse();
        }

        return new GateConnection(simulatorAccount.Name, userId, MaskApiKey(key));
    }

    private static void ApplyConnection(ExchangeAccount account, GateConnection connection)
    {
        var now = DateTimeOffset.UtcNow;
        account.GateSimulatorAccount = connection.SimulatorAccount;
        account.GateSimulatorUserId = connection.SimulatorUserId;
        account.MaskedApiKey = connection.MaskedApiKey;
        account.ConnectedAt = now;
        account.UpdatedAt = now;
    }

    private async Task RequireNoGateAccountAsync()
    {
        var hasGateAccount = await _context.ExchangeAccounts
            .AnyAsync(account => account.WorkspaceId == WorkspaceId && account.ExchangeType == ExchangeTypes.Gate);
        if (hasGateAccount)
        {
            throw GateAccountExists();
        }
    }

    private async Task SaveAccountAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, ExchangeAccount.OneGateAccountPerWorkspaceIndex))
        {
            throw GateAccountExists();
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, ExchangeAccount.OneAccountPerSimulatorAccountIndex))
        {
            throw SimulatorAccountInUse();
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception, string indexName)
    {
        return exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
            && postgres.ConstraintName == indexName;
    }

    private static string MaskApiKey(string apiKey)
    {
        return apiKey.Length <= MaskedKeyEdgeLength * 2
            ? "****"
            : $"{apiKey[..MaskedKeyEdgeLength]}****{apiKey[^MaskedKeyEdgeLength..]}";
    }

    private static string RequireExchangeType(string? exchangeType)
    {
        if (string.IsNullOrWhiteSpace(exchangeType) || !ExchangeTypes.Supported.Contains(exchangeType))
        {
            throw ExchangeOperationException.BadRequest(
                "EXCHANGE_TYPE_NOT_SUPPORTED",
                $"Exchange type {exchangeType} is not supported");
        }

        return exchangeType;
    }

    private static void RequireGateAccountType(string exchangeType)
    {
        if (exchangeType != ExchangeTypes.Gate)
        {
            throw ExchangeOperationException.BadRequest(
                "EXCHANGE_TYPE_NOT_SUPPORTED",
                "Only GATEIO accounts can connect to the Gate simulator");
        }
    }

    private static string RequireName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw ExchangeOperationException.BadRequest("NAME_REQUIRED", "Display name is required");
        }

        if (trimmed.Length > MaxNameLength)
        {
            throw ExchangeOperationException.BadRequest(
                "NAME_TOO_LONG",
                $"Display name must have at most {MaxNameLength} characters");
        }

        return trimmed;
    }

    private static decimal? ParseOptionalAmount(string? value, string fieldName, string errorCode)
    {
        if (value == null)
        {
            return null;
        }

        if (!DecimalColumnGuard.TryParseNonNegativeNumeric36Scale18(value, fieldName, out var amount, out var error))
        {
            throw ExchangeOperationException.BadRequest(errorCode, error!);
        }

        return amount;
    }

    private static ExchangeOperationException AccountNotFound(string exchangeAccountId)
    {
        return ExchangeOperationException.NotFound(
            "EXCHANGE_ACCOUNT_NOT_FOUND",
            $"Exchange account {exchangeAccountId} not found");
    }

    private static ExchangeOperationException SimulatorAccountInUse()
    {
        return AlreadyConnected("This Gate simulator account is already connected. It can be connected in only one workspace");
    }

    private static ExchangeOperationException GateAccountExists()
    {
        return new ExchangeOperationException(
            HttpStatusCode.Conflict,
            "GATE_ACCOUNT_ALREADY_EXISTS",
            "This workspace already has a Gate account. A workspace can have only one Gate account");
    }

    private static ExchangeOperationException AlreadyConnected(string message)
    {
        return new ExchangeOperationException(HttpStatusCode.Conflict, "EXCHANGE_ACCOUNT_ALREADY_CONNECTED", message);
    }

    private sealed record GateConnection(string SimulatorAccount, long SimulatorUserId, string MaskedApiKey);

    private sealed record AssetView(
        string AssetId,
        decimal Custody,
        decimal Locked,
        decimal ExchangeHold,
        decimal Available,
        decimal Credit,
        string? CollateralAddress);

    private sealed record AccountView(ExchangeAccount Account, bool Success, List<AssetView> Assets);
}
