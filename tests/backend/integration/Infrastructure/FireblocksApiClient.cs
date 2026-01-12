using System.Net.Http.Json;
using System.Text.Json;

namespace Waterblocks.IntegrationTests.Infrastructure;

/// <summary>
/// Client for interacting with the Fireblocks-compatible API during tests.
/// Uses X-API-Key header for authentication.
/// </summary>
public class FireblocksApiClient
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _apiKey;

    public FireblocksApiClient(HttpClient client)
    {
        _client = client;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public void SetApiKey(string apiKey)
    {
        _apiKey = apiKey;
        _client.DefaultRequestHeaders.Remove("X-API-Key");
        _client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    }

    public void ClearApiKey()
    {
        _apiKey = null;
        _client.DefaultRequestHeaders.Remove("X-API-Key");
    }

    // Vault Accounts
    public async Task<FireblocksVaultAccountDto?> CreateVaultAccountAsync(CreateVaultAccountRequest request)
    {
        var response = await _client.PostAsJsonAsync("/vault/accounts", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FireblocksVaultAccountDto>(_jsonOptions);
    }

    public async Task<List<FireblocksVaultAccountDto>?> GetVaultAccountsAsync()
    {
        var response = await _client.GetAsync("/vault/accounts");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<FireblocksVaultAccountDto>>(_jsonOptions);
    }

    public async Task<FireblocksVaultAccountDto?> GetVaultAccountAsync(string vaultAccountId)
    {
        var response = await _client.GetAsync($"/vault/accounts/{vaultAccountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FireblocksVaultAccountDto>(_jsonOptions);
    }

    public async Task<HttpResponseMessage> GetVaultAccountRawAsync(string vaultAccountId)
    {
        return await _client.GetAsync($"/vault/accounts/{vaultAccountId}");
    }

    // Wallets
    public async Task<FireblocksCreateVaultAssetResponse?> CreateWalletAsync(string vaultAccountId, string assetId)
    {
        var response = await _client.PostAsync($"/vault/accounts/{vaultAccountId}/{assetId}", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FireblocksCreateVaultAssetResponse>(_jsonOptions);
    }

    public async Task<FireblocksVaultAssetDto?> GetWalletAsync(string vaultAccountId, string assetId)
    {
        var response = await _client.GetAsync($"/vault/accounts/{vaultAccountId}/{assetId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FireblocksVaultAssetDto>(_jsonOptions);
    }

    // Transactions
    public async Task<FireblocksCreateTransactionResponse?> CreateTransactionAsync(FireblocksCreateTransactionRequest request)
    {
        var response = await _client.PostAsJsonAsync("/transactions", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FireblocksCreateTransactionResponse>(_jsonOptions);
    }

    public async Task<List<FireblocksTransactionDto>?> GetTransactionsAsync()
    {
        var response = await _client.GetAsync("/transactions");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<FireblocksTransactionDto>>(_jsonOptions);
    }

    public async Task<FireblocksTransactionDto?> GetTransactionAsync(string txId)
    {
        var response = await _client.GetAsync($"/transactions/{txId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FireblocksTransactionDto>(_jsonOptions);
    }

    public async Task<HttpResponseMessage> GetTransactionRawAsync(string txId)
    {
        return await _client.GetAsync($"/transactions/{txId}");
    }

    public async Task<HttpResponseMessage> GetTransactionsRawAsync()
    {
        return await _client.GetAsync("/transactions");
    }
}

// DTOs for Fireblocks API responses
public class CreateVaultAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public string? CustomerRefId { get; set; }
    public bool AutoFuel { get; set; }
    public bool? HiddenOnUI { get; set; }
}

public class FireblocksVaultAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool HiddenOnUI { get; set; }
    public string CustomerRefId { get; set; } = string.Empty;
    public bool AutoFuel { get; set; }
    public List<FireblocksVaultAssetDto> Assets { get; set; } = new();
}

public class FireblocksVaultAssetDto
{
    public string Id { get; set; } = string.Empty;
    public string Balance { get; set; } = "0";
    public string LockedAmount { get; set; } = "0";
    public string Available { get; set; } = "0";
}

public class FireblocksCreateTransactionRequest
{
    public string AssetId { get; set; } = string.Empty;
    public FireblocksTransferPeerPath? Source { get; set; }
    public FireblocksDestinationTransferPeerPath? Destination { get; set; }
    public string Amount { get; set; } = "0";
    public string? Note { get; set; }
    public string? ExternalTxId { get; set; }
    public string? CustomerRefId { get; set; }
    public string? Operation { get; set; }
    public bool? TreatAsGrossAmount { get; set; }
}

public class FireblocksTransferPeerPath
{
    public string Type { get; set; } = "VAULT_ACCOUNT";
    public string Id { get; set; } = string.Empty;
}

public class FireblocksDestinationTransferPeerPath
{
    public string Type { get; set; } = "ONE_TIME_ADDRESS";
    public string? Id { get; set; }
    public FireblocksOneTimeAddress? OneTimeAddress { get; set; }
}

public class FireblocksOneTimeAddress
{
    public string Address { get; set; } = string.Empty;
    public string? Tag { get; set; }
}

public class FireblocksCreateTransactionResponse
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class FireblocksTransactionDto
{
    public string Id { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public FireblocksTransferPeerPathResponse? Source { get; set; }
    public FireblocksTransferPeerPathResponse? Destination { get; set; }
    public string RequestedAmount { get; set; } = "0";
    public string Amount { get; set; } = "0";
    public string NetAmount { get; set; } = "0";
    public string ServiceFee { get; set; } = "0";
    public string NetworkFee { get; set; } = "0";
    public decimal CreatedAt { get; set; }
    public decimal LastUpdated { get; set; }
    public string Status { get; set; } = string.Empty;
    public string TxHash { get; set; } = string.Empty;
    public string SubStatus { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public string SourceAddress { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string ExternalTxId { get; set; } = string.Empty;
    public string CustomerRefId { get; set; } = string.Empty;
}

public class FireblocksTransferPeerPathResponse
{
    public string Type { get; set; } = string.Empty;
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string SubType { get; set; } = string.Empty;
}

public class FireblocksCreateVaultAssetResponse
{
    public string Id { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string LegacyAddress { get; set; } = string.Empty;
    public string EnterpriseAddress { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string EosAccountName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
