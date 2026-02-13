namespace Waterblocks.Api.Services;

internal static class AddressComparison
{
    public static string Normalize(string? address, bool isCaseSensitive)
    {
        var value = address?.Trim() ?? string.Empty;
        return isCaseSensitive ? value : value.ToLowerInvariant();
    }

    public static bool Equals(string? left, string? right, bool isCaseSensitive)
    {
        var comparison = isCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        return string.Equals(left?.Trim(), right?.Trim(), comparison);
    }

    public static string BuildAssetAddressKey(string assetId, string? address, bool isCaseSensitive)
    {
        return $"{assetId}|{Normalize(address, isCaseSensitive)}";
    }
}
