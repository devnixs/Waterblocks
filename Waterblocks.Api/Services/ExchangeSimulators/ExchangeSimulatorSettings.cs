namespace Waterblocks.Api.Services.ExchangeSimulators;

/// <summary>
/// The optional settings of the simulator of one exchange, keyed by exchange type, for example the environment
/// variable EXCHANGE_SIMULATORS__GATEIO__BASE_URL. BASE_URL fills an empty simulator URL of the exchange catalog at
/// startup. ADMIN_TOKEN is sent as X-Admin-Token, so that the token stays out of the database.
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
