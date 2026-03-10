using System.Globalization;

namespace Waterblocks.Api.Utils;

public static class DecimalColumnGuard
{
    public const int NumericScale = 18;
    public const decimal Numeric36Scale18ExclusiveUpperBound = 1_000_000_000_000_000_000m;

    public static bool TryParsePositiveNumeric36Scale18(
        string? rawValue,
        string fieldName,
        out decimal value,
        out string? errorMessage)
    {
        value = 0m;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            errorMessage = $"{fieldName} is required.";
            return false;
        }

        if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            errorMessage = $"{fieldName} must be a valid decimal number.";
            return false;
        }

        if (value <= 0)
        {
            errorMessage = $"{fieldName} must be greater than zero.";
            return false;
        }

        return TryValidateNumeric36Scale18(value, fieldName, out errorMessage);
    }

    public static bool TryParseNonNegativeNumeric36Scale18(
        string? rawValue,
        string fieldName,
        out decimal value,
        out string? errorMessage)
    {
        value = 0m;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            errorMessage = $"{fieldName} is required.";
            return false;
        }

        if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            errorMessage = $"{fieldName} must be a valid decimal number.";
            return false;
        }

        if (value < 0)
        {
            errorMessage = $"{fieldName} cannot be negative.";
            return false;
        }

        return TryValidateNumeric36Scale18(value, fieldName, out errorMessage);
    }

    public static bool TryValidateNumeric36Scale18(decimal value, string fieldName, out string? errorMessage)
    {
        if (GetEffectiveScale(value) > NumericScale)
        {
            errorMessage = $"{fieldName} has too many decimal places. Maximum supported scale is 18.";
            return false;
        }

        if (decimal.Abs(value) >= Numeric36Scale18ExclusiveUpperBound)
        {
            errorMessage = $"{fieldName} is too large. Maximum supported value is 999999999999999999.999999999999999999.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public static void EnsureNumeric36Scale18(decimal value, string fieldName)
    {
        if (!TryValidateNumeric36Scale18(value, fieldName, out var errorMessage))
        {
            throw new UnsafeNumericValueException(errorMessage!);
        }
    }

    private static int GetEffectiveScale(decimal value)
    {
        var rawScale = (decimal.GetBits(value)[3] >> 16) & 0x7F;
        var normalized = value.ToString($"F{rawScale}", CultureInfo.InvariantCulture)
            .TrimEnd('0')
            .TrimEnd('.');
        var separatorIndex = normalized.IndexOf('.');
        return separatorIndex < 0
            ? 0
            : normalized.Length - separatorIndex - 1;
    }
}

public sealed class UnsafeNumericValueException : InvalidOperationException
{
    public UnsafeNumericValueException(string message)
        : base(message)
    {
    }
}
