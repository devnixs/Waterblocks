namespace Waterblocks.Api.Infrastructure;

public sealed class DuplicateExternalTxIdException : Exception
{
    public DuplicateExternalTxIdException(string externalTxId)
        : base($"External transaction id '{externalTxId}' already exists")
    {
        ExternalTxId = externalTxId;
    }

    public string ExternalTxId { get; }
}
