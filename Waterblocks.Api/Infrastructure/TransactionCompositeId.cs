namespace Waterblocks.Api.Infrastructure;

public static class TransactionCompositeId
{
    private const string Separator = "::";

    public static string Build(string? workspaceId, string transactionId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return transactionId;
        }

        return $"{workspaceId}{Separator}{transactionId}";
    }

    public static bool TryParse(string compositeId, out string workspaceId, out string transactionId)
    {
        workspaceId = string.Empty;
        transactionId = string.Empty;

        if (string.IsNullOrWhiteSpace(compositeId))
        {
            return false;
        }

        var index = compositeId.IndexOf(Separator, StringComparison.Ordinal);
        if (index <= 0 || index + Separator.Length >= compositeId.Length)
        {
            return false;
        }

        workspaceId = compositeId.Substring(0, index);
        transactionId = compositeId.Substring(index + Separator.Length);
        return true;
    }

    public static bool TryUnwrap(string compositeId, string? expectedWorkspaceId, out string transactionId)
    {
        transactionId = compositeId;

        if (!TryParse(compositeId, out var workspaceId, out var rawId))
        {
            return true;
        }

        transactionId = rawId;

        if (string.IsNullOrWhiteSpace(expectedWorkspaceId))
        {
            return true;
        }

        return string.Equals(workspaceId, expectedWorkspaceId, StringComparison.Ordinal);
    }
}
