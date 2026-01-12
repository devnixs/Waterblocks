namespace Waterblocks.Api.Services;

public interface IAddressGenerator
{
    string GenerateAdminDepositAddress(string assetId, string vaultAccountId);
    string GenerateVaultWalletDepositAddress(string assetId, string vaultAccountId);
    string GenerateExternalAddress(string assetId);
    AddressGenerationResult GenerateVaultAddress(string assetId, int addressIndex);
}

public sealed record AddressGenerationResult(
    string AddressValue,
    string AddressFormat,
    string? LegacyAddress,
    string? EnterpriseAddress);

public sealed class AddressGenerator : IAddressGenerator
{
    public string GenerateAdminDepositAddress(string assetId, string vaultAccountId)
    {
        return $"{assetId.ToLowerInvariant()}_{vaultAccountId[..8]}_{Guid.NewGuid():N}";
    }

    public string GenerateVaultWalletDepositAddress(string assetId, string vaultAccountId)
    {
        return assetId.ToUpperInvariant() switch
        {
            "BTC" => $"bc1q{Guid.NewGuid():N}"[..42],
            "ETH" or "USDT" or "USDC" => $"0x{Guid.NewGuid():N}{Guid.NewGuid():N}"[..42],
            _ => $"{assetId.ToLowerInvariant()}_{vaultAccountId[..Math.Min(8, vaultAccountId.Length)]}_{Guid.NewGuid():N}"
        };
    }

    public string GenerateExternalAddress(string assetId)
    {
        var addressFormat = DetermineAddressFormat(assetId);
        return GenerateAddress(assetId, addressFormat);
    }

    public AddressGenerationResult GenerateVaultAddress(string assetId, int addressIndex)
    {
        _ = addressIndex;
        var addressFormat = DetermineAddressFormat(assetId);
        var addressValue = GenerateAddress(assetId, addressFormat);
        var legacyAddress = GenerateLegacyAddress(assetId, addressFormat);
        var enterpriseAddress = GenerateEnterpriseAddress(assetId);
        return new AddressGenerationResult(addressValue, addressFormat, legacyAddress, enterpriseAddress);
    }

    private static string DetermineAddressFormat(string assetId)
    {
        return assetId.ToUpperInvariant() switch
        {
            "BTC" => "SEGWIT",
            "ETH" or "USDT" or "USDC" => "BASE",
            _ => "BASE",
        };
    }

    private static string GenerateAddress(string assetId, string addressFormat)
    {
        return assetId.ToUpperInvariant() switch
        {
            "BTC" when addressFormat == "SEGWIT" => $"bc1q{Guid.NewGuid():N}"[..42],
            "BTC" => $"1{Guid.NewGuid():N}"[..34],
            "ETH" or "USDT" or "USDC" => $"0x{Guid.NewGuid():N}{Guid.NewGuid():N}"[..42],
            _ => $"{assetId.ToLowerInvariant()}_{Guid.NewGuid():N}",
        };
    }

    private static string? GenerateLegacyAddress(string assetId, string addressFormat)
    {
        if (assetId.ToUpperInvariant() == "BTC" && addressFormat == "SEGWIT")
        {
            return $"1{Guid.NewGuid():N}"[..34];
        }
        return null;
    }

    private static string? GenerateEnterpriseAddress(string assetId)
    {
        return assetId.ToUpperInvariant() switch
        {
            "ETH" or "USDT" or "USDC" => $"0xE{Guid.NewGuid():N}{Guid.NewGuid():N}"[..42],
            _ => null,
        };
    }
}
