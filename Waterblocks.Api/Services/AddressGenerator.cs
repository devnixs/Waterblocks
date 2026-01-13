using System.Security.Cryptography;
using System.Text;
using CardanoSharp.Wallet;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Models.Addresses;
using CardanoSharp.Wallet.Models.Keys;
using NBitcoin;
using Nethereum.Util;

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
    private const int MaxGenerationAttempts = 5;
    // Character sets for different address formats
    private const string HexChars = "0123456789abcdef";
    private const string Base58Chars = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const string Bech32Chars = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";
    private static readonly HashSet<string> EvmAssets = new(StringComparer.OrdinalIgnoreCase)
    {
        "ETH",
        "USDT",
        "USDC",
        "USDT_ERC20",
        "USDC_ERC20",
        "MATIC",
        "MATIC_POLYGON",
        "BNB",
        "BNB_BSC",
        "AVAX",
        "AVAX_C",
        "BASECHAIN_ETH",
    };
    private readonly IAddressValidationService _validator;

    public AddressGenerator(IAddressValidationService validator)
    {
        _validator = validator;
    }

    public string GenerateAdminDepositAddress(string assetId, string vaultAccountId)
    {
        // Admin addresses use real format so they pass validation
        return GenerateVaultWalletDepositAddress(assetId, vaultAccountId);
    }

    public string GenerateVaultWalletDepositAddress(string assetId, string vaultAccountId)
    {
        return GenerateValidAddress(assetId, DetermineAddressFormat(assetId));
    }

    public string GenerateExternalAddress(string assetId)
    {
        return GenerateValidAddress(assetId, DetermineAddressFormat(assetId));
    }

    public AddressGenerationResult GenerateVaultAddress(string assetId, int addressIndex)
    {
        _ = addressIndex;
        var addressFormat = DetermineAddressFormat(assetId);
        var addressValue = GenerateValidAddress(assetId, addressFormat);
        var legacyAddress = GenerateValidLegacyAddress(assetId, addressFormat);
        var enterpriseAddress = GenerateValidEnterpriseAddress(assetId);
        return new AddressGenerationResult(addressValue, addressFormat, legacyAddress, enterpriseAddress);
    }

    private static string DetermineAddressFormat(string assetId)
    {
        return assetId.ToUpperInvariant() switch
        {
            "BTC" => "SEGWIT",
            "LTC" => "SEGWIT",
            "ETH" or "USDT" or "USDC" or "USDT_ERC20" or "USDC_ERC20" => "BASE",
            "SOL" or "SOL_TEST" => "BASE",
            "XRP" or "XRP_TEST" => "BASE",
            "DOGE" => "BASE",
            "TRX" or "USDT_TRC20" => "BASE",
            "MATIC" or "MATIC_POLYGON" => "BASE",
            "AVAX" or "AVAX_C" => "BASE",
            "BNB" or "BNB_BSC" => "BASE",
            "ADA" => "SHELLEY",
            _ => "BASE",
        };
    }

    private static string GenerateAddress(string assetId, string addressFormat)
    {
        var upperAsset = assetId.ToUpperInvariant();

        return upperAsset switch
        {
            // Bitcoin - Bech32 SegWit (bc1q...) - 42 characters
            "BTC" when addressFormat == "SEGWIT" => GenerateBtcSegwitAddress(),
            // Bitcoin - Legacy P2PKH (1...) - 34 characters
            "BTC" => GenerateBtcLegacyAddress(),

            // Litecoin - Bech32 SegWit (ltc1q...) - 43 characters
            "LTC" when addressFormat == "SEGWIT" => GenerateLtcSegwitAddress(),
            // Litecoin - Legacy (L...) - 34 characters
            "LTC" => GenerateLtcLegacyAddress(),

            // Ethereum and ERC-20 tokens - 42 characters (0x + 40 hex)
            "ETH" or "USDT" or "USDC" or "USDT_ERC20" or "USDC_ERC20" => GenerateEthAddress(),

            // Solana - Base58, 44 characters
            "SOL" or "SOL_TEST" => GenerateSolAddress(),

            // Ripple/XRP - Base58 starting with 'r', 25-35 characters
            "XRP" or "XRP_TEST" => GenerateXrpAddress(),

            // Dogecoin - Base58 starting with 'D', 34 characters
            "DOGE" => GenerateDogeAddress(),

            // Tron and TRC-20 tokens - Base58 starting with 'T', 34 characters
            "TRX" or "USDT_TRC20" => GenerateTronAddress(),

            // Polygon/MATIC - Same as ETH (EVM compatible)
            "MATIC" or "MATIC_POLYGON" => GenerateEthAddress(),

            // Avalanche C-Chain - Same as ETH (EVM compatible)
            "AVAX" or "AVAX_C" => GenerateEthAddress(),

            // BNB Smart Chain - Same as ETH (EVM compatible)
            "BNB" or "BNB_BSC" => GenerateEthAddress(),

            // Cardano - Bech32 Shelley address (addr1...)
            "ADA" => GenerateCardanoAddress(),

            // Cosmos/ATOM - Bech32 (cosmos1...)
            "ATOM" => GenerateCosmosAddress("cosmos"),

            // Polkadot - SS58 format, starts with 1
            "DOT" => GeneratePolkadotAddress(),

            // Near Protocol - human readable account or implicit (64 hex chars)
            "NEAR" => GenerateNearAddress(),

            // Stellar - Base32 starting with 'G', 56 characters
            "XLM" => GenerateStellarAddress(),

            // Algorand - Base32, 58 characters
            "ALGO" => GenerateAlgorandAddress(),

            // Default: generate a hex-based address that looks like ETH
            _ => GenerateEthAddress(),
        };
    }

    private string GenerateValidAddress(string assetId, string addressFormat)
    {
        for (var i = 0; i < MaxGenerationAttempts; i++)
        {
            var address = GenerateAddress(assetId, addressFormat);
            if (_validator.ValidateAddress(assetId, address))
            {
                return NormalizeAddress(assetId, address);
            }
        }

        return NormalizeAddress(assetId, GenerateAddress(assetId, addressFormat));
    }

    private static string? GenerateLegacyAddress(string assetId, string addressFormat)
    {
        return assetId.ToUpperInvariant() switch
        {
            "BTC" when addressFormat == "SEGWIT" => GenerateBtcLegacyAddress(),
            "LTC" when addressFormat == "SEGWIT" => GenerateLtcLegacyAddress(),
            _ => null,
        };
    }

    private string? GenerateValidLegacyAddress(string assetId, string addressFormat)
    {
        var legacy = GenerateLegacyAddress(assetId, addressFormat);
        if (string.IsNullOrEmpty(legacy))
        {
            return legacy;
        }

        for (var i = 0; i < MaxGenerationAttempts; i++)
        {
            if (!string.IsNullOrEmpty(legacy) && _validator.ValidateAddress(assetId, legacy))
            {
                return NormalizeAddress(assetId, legacy);
            }
            legacy = GenerateLegacyAddress(assetId, addressFormat);
        }

        return string.IsNullOrEmpty(legacy) ? legacy : NormalizeAddress(assetId, legacy);
    }

    private static string? GenerateEnterpriseAddress(string assetId)
    {
        return assetId.ToUpperInvariant() switch
        {
            "ETH" or "USDT" or "USDC" or "USDT_ERC20" or "USDC_ERC20" => GenerateEthAddress(),
            _ => null,
        };
    }

    private string? GenerateValidEnterpriseAddress(string assetId)
    {
        var enterprise = GenerateEnterpriseAddress(assetId);
        if (string.IsNullOrEmpty(enterprise))
        {
            return enterprise;
        }

        for (var i = 0; i < MaxGenerationAttempts; i++)
        {
            if (!string.IsNullOrEmpty(enterprise) && _validator.ValidateAddress(assetId, enterprise))
            {
                return NormalizeAddress(assetId, enterprise);
            }
            enterprise = GenerateEnterpriseAddress(assetId);
        }

        return string.IsNullOrEmpty(enterprise) ? enterprise : NormalizeAddress(assetId, enterprise);
    }

    private static string NormalizeAddress(string assetId, string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return address;
        }

        var upperAsset = assetId.ToUpperInvariant();
        try
        {
            if (EvmAssets.Contains(upperAsset))
            {
                return AddressUtil.Current.ConvertToChecksumAddress(address);
            }

            if (upperAsset == "ADA")
            {
                var bytes = new Address(address).GetBytes();
                return new Address(bytes).ToString();
            }
        }
        catch
        {
            return address;
        }

        return address;
    }

    // Bitcoin SegWit (Bech32) - bc1q + 39 bech32 chars = 42 total
    private static string GenerateBtcSegwitAddress()
    {
        var key = new Key();
        return key.GetAddress(ScriptPubKeyType.Segwit, Network.Main).ToString();
    }

    // Bitcoin Legacy P2PKH - 1 + 33 base58 chars = 34 total
    private static string GenerateBtcLegacyAddress()
    {
        var key = new Key();
        return key.GetAddress(ScriptPubKeyType.Legacy, Network.Main).ToString();
    }

    // Bitcoin P2SH - 3 + 33 base58 chars = 34 total
    private static string GenerateBtcP2shAddress()
    {
        return "3" + GenerateRandomString(Base58Chars, 33);
    }

    // Litecoin SegWit - ltc1q + 39 bech32 chars = 43 total
    private static string GenerateLtcSegwitAddress()
    {
        return "ltc1q" + GenerateRandomString(Bech32Chars, 38);
    }

    // Litecoin Legacy - L + 33 base58 chars = 34 total
    private static string GenerateLtcLegacyAddress()
    {
        return "L" + GenerateRandomString(Base58Chars, 33);
    }

    // Ethereum - 0x + 40 hex chars = 42 total
    private static string GenerateEthAddress()
    {
        return "0x" + GenerateRandomString(HexChars, 40);
    }

    // Solana - 44 base58 characters
    private static string GenerateSolAddress()
    {
        return GenerateRandomString(Base58Chars, 44);
    }

    // XRP/Ripple - r + 24-33 base58 chars (we use 33 for consistency)
    private static string GenerateXrpAddress()
    {
        return "r" + GenerateRandomString(Base58Chars, 33);
    }

    // Dogecoin - D + 33 base58 chars = 34 total
    private static string GenerateDogeAddress()
    {
        return "D" + GenerateRandomString(Base58Chars, 33);
    }

    // Tron - T + 33 base58 chars = 34 total
    private static string GenerateTronAddress()
    {
        return "T" + GenerateRandomString(Base58Chars, 33);
    }

    // Cardano Shelley - addr1 + bech32 chars (typically 98+ chars, we use 54 for data part)
    private static string GenerateCardanoAddress()
    {
        var rng = RandomNumberGenerator.Create();
        var keyBytes = new byte[32];
        var chainCode = new byte[32];
        rng.GetBytes(keyBytes);
        rng.GetBytes(chainCode);

        var publicKey = new PublicKey(keyBytes, chainCode);
        var addressService = new AddressService();
        var address = addressService.GetEnterpriseAddress(publicKey, NetworkType.Mainnet);
        return address.ToString();
    }

    // Cosmos-based chains - {prefix}1 + bech32 chars
    private static string GenerateCosmosAddress(string prefix)
    {
        return prefix + "1" + GenerateRandomString(Bech32Chars, 38);
    }

    // Polkadot SS58 - starts with 1, base58 encoded
    private static string GeneratePolkadotAddress()
    {
        return "1" + GenerateRandomString(Base58Chars, 47);
    }

    // Near Protocol - 64 hex characters (implicit account)
    private static string GenerateNearAddress()
    {
        return GenerateRandomString(HexChars, 64);
    }

    // Stellar - G + 55 base32 uppercase chars = 56 total
    private static string GenerateStellarAddress()
    {
        const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        return "G" + GenerateRandomString(base32Chars, 55);
    }

    // Algorand - 58 base32 uppercase chars
    private static string GenerateAlgorandAddress()
    {
        const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        return GenerateRandomString(base32Chars, 58);
    }

    private static string GenerateRandomString(string charset, int length)
    {
        var sb = new StringBuilder(length);
        var bytes = RandomNumberGenerator.GetBytes(length);

        for (int i = 0; i < length; i++)
        {
            sb.Append(charset[bytes[i] % charset.Length]);
        }

        return sb.ToString();
    }
}
