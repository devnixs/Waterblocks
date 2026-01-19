using System.Security.Cryptography;
using System.Text;

namespace Waterblocks.Api.Utils;

/// <summary>
/// Generates realistic-looking blockchain transaction hashes for different blockchain types.
/// These are fake hashes for testing purposes only - not cryptographically derived from actual transaction data.
/// </summary>
public static class TransactionHashGenerator
{
    /// <summary>
    /// Generates a blockchain-appropriate transaction hash based on the asset's blockchain type.
    /// </summary>
    /// <param name="assetId">The asset ID (e.g., "BTC", "ETH", "USDC")</param>
    /// <param name="blockchainType">The type of blockchain</param>
    /// <returns>A realistic-looking transaction hash for the given blockchain type</returns>
    public static string Generate(string assetId, Models.BlockchainType blockchainType)
    {
        return blockchainType switch
        {
            Models.BlockchainType.AccountBased => GenerateEthereumStyleHash(),
            Models.BlockchainType.AddressBased => GenerateBitcoinStyleHash(),
            Models.BlockchainType.MemoBased => GenerateEthereumStyleHash(), // XRP/XLM use similar format
            _ => GenerateEthereumStyleHash()
        };
    }

    /// <summary>
    /// Generates an Ethereum-style transaction hash (0x + 64 hex characters).
    /// Example: 0x1a2b3c4d5e6f7890abcdef1234567890abcdef1234567890abcdef1234567890
    /// </summary>
    private static string GenerateEthereumStyleHash()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32); // 32 bytes = 64 hex characters
        var hexString = Convert.ToHexString(randomBytes).ToLowerInvariant();
        return $"0x{hexString}";
    }

    /// <summary>
    /// Generates a Bitcoin-style transaction hash (64 hex characters, no prefix).
    /// Example: 1a2b3c4d5e6f7890abcdef1234567890abcdef1234567890abcdef1234567890
    /// </summary>
    private static string GenerateBitcoinStyleHash()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32); // 32 bytes = 64 hex characters
        return Convert.ToHexString(randomBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a deterministic-looking hash from a seed (for testing purposes).
    /// This creates variation while being reproducible if needed for debugging.
    /// </summary>
    /// <param name="seed">A seed string (e.g., transaction ID)</param>
    /// <param name="blockchainType">The type of blockchain</param>
    /// <returns>A deterministic hash based on the seed</returns>
    public static string GenerateFromSeed(string seed, Models.BlockchainType blockchainType)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var hexString = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return blockchainType switch
        {
            Models.BlockchainType.AccountBased => $"0x{hexString}",
            Models.BlockchainType.AddressBased => hexString,
            Models.BlockchainType.MemoBased => $"0x{hexString}",
            _ => $"0x{hexString}"
        };
    }
}
