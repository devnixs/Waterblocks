using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FireblocksReplacement.IntegrationTests.Infrastructure;

/// <summary>
/// Client for interacting with the Admin API during tests.
/// Provides strongly-typed methods for common operations.
/// </summary>
public class AdminApiClient
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public AdminApiClient(HttpClient client)
    {
        _client = client;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public void SetWorkspace(string workspaceId)
    {
        _client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        _client.DefaultRequestHeaders.Add("X-Workspace-Id", workspaceId);
    }

    // Workspaces
    public async Task<AdminResponse<WorkspaceDto>> CreateWorkspaceAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/admin/workspaces", new { name });
        return await DeserializeResponse<WorkspaceDto>(response);
    }

    public async Task<AdminResponse<List<WorkspaceDto>>> GetWorkspacesAsync()
    {
        var response = await _client.GetAsync("/admin/workspaces");
        return await DeserializeResponse<List<WorkspaceDto>>(response);
    }

    // Vaults
    public async Task<AdminResponse<VaultDto>> CreateVaultAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/admin/vaults", new { name });
        return await DeserializeResponse<VaultDto>(response);
    }

    public async Task<AdminResponse<VaultDto>> GetVaultAsync(string vaultId)
    {
        var response = await _client.GetAsync($"/admin/vaults/{vaultId}");
        return await DeserializeResponse<VaultDto>(response);
    }

    public async Task<AdminResponse<List<VaultDto>>> GetVaultsAsync()
    {
        var response = await _client.GetAsync("/admin/vaults");
        return await DeserializeResponse<List<VaultDto>>(response);
    }

    // Wallets
    public async Task<AdminResponse<WalletDto>> CreateWalletAsync(string vaultId, string assetId)
    {
        var response = await _client.PostAsJsonAsync($"/admin/vaults/{vaultId}/wallets", new { assetId });
        return await DeserializeResponse<WalletDto>(response);
    }

    // Transactions
    public async Task<AdminResponse<TransactionDto>> CreateTransactionAsync(CreateTransactionRequest request)
    {
        var response = await _client.PostAsJsonAsync("/admin/transactions", request);
        return await DeserializeResponse<TransactionDto>(response);
    }

    public async Task<AdminResponse<TransactionDto>> GetTransactionAsync(string transactionId)
    {
        var response = await _client.GetAsync($"/admin/transactions/{transactionId}");
        return await DeserializeResponse<TransactionDto>(response);
    }

    public async Task<AdminResponse<List<TransactionDto>>> GetTransactionsAsync()
    {
        var response = await _client.GetAsync("/admin/transactions");
        return await DeserializeResponse<List<TransactionDto>>(response);
    }

    // State transitions
    public async Task<AdminResponse<TransactionStateDto>> ApproveTransactionAsync(string transactionId)
    {
        var response = await _client.PostAsync($"/admin/transactions/{transactionId}/approve", null);
        return await DeserializeResponse<TransactionStateDto>(response);
    }

    public async Task<AdminResponse<TransactionStateDto>> SignTransactionAsync(string transactionId)
    {
        var response = await _client.PostAsync($"/admin/transactions/{transactionId}/sign", null);
        return await DeserializeResponse<TransactionStateDto>(response);
    }

    public async Task<AdminResponse<TransactionStateDto>> BroadcastTransactionAsync(string transactionId)
    {
        var response = await _client.PostAsync($"/admin/transactions/{transactionId}/broadcast", null);
        return await DeserializeResponse<TransactionStateDto>(response);
    }

    public async Task<AdminResponse<TransactionStateDto>> ConfirmTransactionAsync(string transactionId)
    {
        var response = await _client.PostAsync($"/admin/transactions/{transactionId}/confirm", null);
        return await DeserializeResponse<TransactionStateDto>(response);
    }

    public async Task<AdminResponse<TransactionStateDto>> CompleteTransactionAsync(string transactionId)
    {
        var response = await _client.PostAsync($"/admin/transactions/{transactionId}/complete", null);
        return await DeserializeResponse<TransactionStateDto>(response);
    }

    public async Task<AdminResponse<TransactionStateDto>> FailTransactionAsync(string transactionId, string? reason = null)
    {
        var response = await _client.PostAsJsonAsync($"/admin/transactions/{transactionId}/fail", new { reason });
        return await DeserializeResponse<TransactionStateDto>(response);
    }

    public async Task<AdminResponse<TransactionStateDto>> CancelTransactionAsync(string transactionId)
    {
        var response = await _client.PostAsync($"/admin/transactions/{transactionId}/cancel", null);
        return await DeserializeResponse<TransactionStateDto>(response);
    }

    /// <summary>
    /// Runs a transaction through the full lifecycle from SUBMITTED to COMPLETED.
    /// </summary>
    public async Task<AdminResponse<TransactionStateDto>> CompleteTransactionFullCycleAsync(string transactionId)
    {
        var result = await ApproveTransactionAsync(transactionId);
        if (result.Error != null) return result;

        result = await SignTransactionAsync(transactionId);
        if (result.Error != null) return result;

        result = await BroadcastTransactionAsync(transactionId);
        if (result.Error != null) return result;

        result = await ConfirmTransactionAsync(transactionId);
        if (result.Error != null) return result;

        return await CompleteTransactionAsync(transactionId);
    }

    private async Task<AdminResponse<T>> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AdminResponse<T>>(content, _jsonOptions)
            ?? new AdminResponse<T> { Error = new AdminError { Message = "Failed to deserialize response", Code = "DESERIALIZATION_ERROR" } };
    }
}

// DTOs for API responses
public class AdminResponse<T>
{
    public T? Data { get; set; }
    public AdminError? Error { get; set; }

    public bool IsSuccess => Error == null && Data != null;
}

public class AdminError
{
    public string Message { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class WorkspaceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ApiKeyDto> ApiKeys { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ApiKeyDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class VaultDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool HiddenOnUI { get; set; }
    public string? CustomerRefId { get; set; }
    public bool AutoFuel { get; set; }
    public List<WalletDto> Wallets { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WalletDto
{
    public string AssetId { get; set; } = string.Empty;
    public string Balance { get; set; } = "0";
    public string LockedAmount { get; set; } = "0";
    public string Available { get; set; } = "0";
    public string Pending { get; set; } = "0";
    public int AddressCount { get; set; }
    public string? DepositAddress { get; set; }
}

public class TransactionDto
{
    public string Id { get; set; } = string.Empty;
    public string VaultAccountId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? SourceAddress { get; set; }
    public string? SourceVaultAccountId { get; set; }
    public string? SourceVaultAccountName { get; set; }
    public string DestinationType { get; set; } = string.Empty;
    public string? DestinationVaultAccountId { get; set; }
    public string? DestinationVaultAccountName { get; set; }
    public string Amount { get; set; } = "0";
    public string DestinationAddress { get; set; } = string.Empty;
    public string? DestinationTag { get; set; }
    public string State { get; set; } = string.Empty;
    public string? Hash { get; set; }
    public string Fee { get; set; } = "0";
    public string NetworkFee { get; set; } = "0";
    public bool IsFrozen { get; set; }
    public string? FailureReason { get; set; }
    public string? ReplacedByTxId { get; set; }
    public int Confirmations { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TransactionStateDto
{
    public string Id { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

public class CreateTransactionRequest
{
    public string AssetId { get; set; } = string.Empty;
    public string SourceType { get; set; } = "INTERNAL";
    public string? SourceAddress { get; set; }
    public string? SourceVaultAccountId { get; set; }
    public string DestinationType { get; set; } = "EXTERNAL";
    public string? DestinationAddress { get; set; }
    public string? DestinationVaultAccountId { get; set; }
    public string Amount { get; set; } = "0";
    public string? DestinationTag { get; set; }
    public string? InitialState { get; set; }
}
