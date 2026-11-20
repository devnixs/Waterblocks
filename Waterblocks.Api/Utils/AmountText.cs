using System.Globalization;

namespace Waterblocks.Api.Utils;

public static class AmountText
{
    private const string PlainFormat = "0.##################";

    /// <summary>
    /// Writes an amount as an invariant decimal string without an exponent.
    /// </summary>
    public static string Format(decimal value)
    {
        return value.ToString(PlainFormat, CultureInfo.InvariantCulture);
    }
}
