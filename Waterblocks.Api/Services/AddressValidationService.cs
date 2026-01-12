namespace Waterblocks.Api.Services;

public interface IAddressValidationService
{
    bool ValidateAddress(string assetId, string address);
    bool RequiresTag(string assetId);
}

public sealed class AddressValidationService : IAddressValidationService
{
    public bool ValidateAddress(string assetId, string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var upperAsset = assetId.ToUpperInvariant();

        return upperAsset switch
        {
            // Bitcoin - Bech32 (bc1q/bc1p), Legacy P2PKH (1), or P2SH (3)
            "BTC" => ValidateBtcAddress(address),

            // Litecoin - Bech32 (ltc1), Legacy (L), or P2SH (M/3)
            "LTC" => ValidateLtcAddress(address),

            // Ethereum and EVM-compatible chains (0x + 40 hex chars = 42 total)
            "ETH" or "USDT" or "USDC" or "USDT_ERC20" or "USDC_ERC20"
                or "MATIC" or "MATIC_POLYGON"
                or "AVAX" or "AVAX_C"
                or "BNB" or "BNB_BSC" => ValidateEthAddress(address),

            // Solana - Base58, 32-44 characters
            "SOL" or "SOL_TEST" => ValidateSolAddress(address),

            // Ripple/XRP - starts with 'r', 25-35 characters
            "XRP" or "XRP_TEST" => ValidateXrpAddress(address),

            // Dogecoin - starts with 'D' or 'A', 34 characters
            "DOGE" => ValidateDogeAddress(address),

            // Tron - starts with 'T', 34 characters (Base58Check)
            "TRX" or "USDT_TRC20" => ValidateTronAddress(address),

            // Cardano - Bech32 Shelley (addr1) or Byron (Ae2/DdzFF)
            "ADA" => ValidateCardanoAddress(address),

            // Cosmos/ATOM - Bech32 (cosmos1)
            "ATOM" => ValidateCosmosAddress(address, "cosmos"),

            // Polkadot - SS58 format, starts with 1
            "DOT" => ValidatePolkadotAddress(address),

            // Near Protocol - account name or 64 hex chars
            "NEAR" => ValidateNearAddress(address),

            // Stellar - Base32 starting with 'G', 56 characters
            "XLM" => ValidateStellarAddress(address),

            // Algorand - Base32, 58 characters
            "ALGO" => ValidateAlgorandAddress(address),

            // Default: just check it's not empty
            _ => !string.IsNullOrWhiteSpace(address),
        };
    }

    public bool RequiresTag(string assetId)
    {
        var upperAsset = assetId.ToUpperInvariant();
        return upperAsset is "XRP" or "XRP_TEST" or "XLM" or "ATOM" or "EOS";
    }

    private static bool ValidateBtcAddress(string address)
    {
        // Bech32 SegWit (42-62 chars)
        if (address.StartsWith("bc1q") || address.StartsWith("bc1p"))
        {
            return address.Length >= 42 && address.Length <= 62 && IsValidBech32(address[4..]);
        }

        // Legacy P2PKH (starts with 1, 26-35 chars)
        if (address.StartsWith('1'))
        {
            return address.Length >= 26 && address.Length <= 35 && IsValidBase58(address);
        }

        // P2SH (starts with 3, 26-35 chars)
        if (address.StartsWith('3'))
        {
            return address.Length >= 26 && address.Length <= 35 && IsValidBase58(address);
        }

        return false;
    }

    private static bool ValidateLtcAddress(string address)
    {
        // Bech32 SegWit
        if (address.StartsWith("ltc1q") || address.StartsWith("ltc1p"))
        {
            return address.Length >= 43 && address.Length <= 63 && IsValidBech32(address[5..]);
        }

        // Legacy (L or M prefix)
        if (address.StartsWith('L') || address.StartsWith('M'))
        {
            return address.Length >= 26 && address.Length <= 35 && IsValidBase58(address);
        }

        // P2SH (3 prefix, same as BTC)
        if (address.StartsWith('3'))
        {
            return address.Length >= 26 && address.Length <= 35 && IsValidBase58(address);
        }

        return false;
    }

    private static bool ValidateEthAddress(string address)
    {
        if (!address.StartsWith("0x") && !address.StartsWith("0X"))
        {
            return false;
        }

        if (address.Length != 42)
        {
            return false;
        }

        // Check that the rest is valid hex
        return IsValidHex(address[2..]);
    }

    private static bool ValidateSolAddress(string address)
    {
        // Solana addresses are 32-44 base58 characters
        return address.Length >= 32 && address.Length <= 44 && IsValidBase58(address);
    }

    private static bool ValidateXrpAddress(string address)
    {
        // XRP addresses start with 'r' and are 25-35 characters
        return address.StartsWith('r') && address.Length >= 25 && address.Length <= 35 && IsValidBase58(address);
    }

    private static bool ValidateDogeAddress(string address)
    {
        // Dogecoin addresses start with 'D' or 'A' (for multisig)
        return (address.StartsWith('D') || address.StartsWith('A'))
               && address.Length >= 26 && address.Length <= 35
               && IsValidBase58(address);
    }

    private static bool ValidateTronAddress(string address)
    {
        // Tron addresses start with 'T' and are 34 characters
        return address.StartsWith('T') && address.Length == 34 && IsValidBase58(address);
    }

    private static bool ValidateCardanoAddress(string address)
    {
        // Shelley addresses (Bech32)
        if (address.StartsWith("addr1"))
        {
            return address.Length >= 59 && IsValidBech32(address[5..]);
        }

        // Byron addresses (Base58)
        if (address.StartsWith("Ae2") || address.StartsWith("DdzFF"))
        {
            return address.Length >= 50 && IsValidBase58(address);
        }

        return false;
    }

    private static bool ValidateCosmosAddress(string address, string expectedPrefix)
    {
        var prefix = expectedPrefix + "1";
        return address.StartsWith(prefix) && address.Length >= 39 && IsValidBech32(address[prefix.Length..]);
    }

    private static bool ValidatePolkadotAddress(string address)
    {
        // SS58 format, typically starts with 1 for Polkadot mainnet
        return address.StartsWith('1') && address.Length >= 47 && address.Length <= 48 && IsValidBase58(address);
    }

    private static bool ValidateNearAddress(string address)
    {
        // Implicit accounts are 64 hex characters
        if (address.Length == 64 && IsValidHex(address))
        {
            return true;
        }

        // Named accounts (human readable)
        // Must be 2-64 chars, lowercase alphanumeric, can contain - _ .
        if (address.Length >= 2 && address.Length <= 64)
        {
            foreach (var c in address)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
                {
                    return false;
                }
            }
            return true;
        }

        return false;
    }

    private static bool ValidateStellarAddress(string address)
    {
        // Stellar addresses start with 'G' and are 56 characters (Base32)
        return address.StartsWith('G') && address.Length == 56 && IsValidBase32(address);
    }

    private static bool ValidateAlgorandAddress(string address)
    {
        // Algorand addresses are 58 Base32 characters
        return address.Length == 58 && IsValidBase32(address);
    }

    private static bool IsValidHex(string s)
    {
        foreach (var c in s)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsValidBase58(string s)
    {
        const string base58Chars = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        foreach (var c in s)
        {
            if (!base58Chars.Contains(c))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsValidBech32(string s)
    {
        const string bech32Chars = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";
        foreach (var c in s)
        {
            if (!bech32Chars.Contains(c))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsValidBase32(string s)
    {
        const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        foreach (var c in s)
        {
            if (!base32Chars.Contains(c))
            {
                return false;
            }
        }
        return true;
    }
}
