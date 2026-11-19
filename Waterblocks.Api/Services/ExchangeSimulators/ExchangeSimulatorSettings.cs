namespace Waterblocks.Api.Services.ExchangeSimulators;

/// <summary>
/// The configuration keys of an exchange simulator. Each exchange type has its own section,
/// for example the environment variable EXCHANGE_SIMULATORS__GATEIO__BASE_URL.
/// </summary>
public static class ExchangeSimulatorSettings
{
    public const string Section = "EXCHANGE_SIMULATORS";
    public const string BaseUrlName = "BASE_URL";
    public const string AdminTokenName = "ADMIN_TOKEN";

    public static string BaseUrlKey(string exchangeType)
    {
        return $"{Section}:{exchangeType}:{BaseUrlName}";
    }

    public static string AdminTokenKey(string exchangeType)
    {
        return $"{Section}:{exchangeType}:{AdminTokenName}";
    }
}
