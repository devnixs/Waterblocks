using System.Text.Json.Serialization;

namespace Waterblocks.Api.Dtos.Fireblocks;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransferPeerType
{
    VAULT_ACCOUNT,
    ONE_TIME_ADDRESS,
}
