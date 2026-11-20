using System.Net;

namespace Waterblocks.Api.Infrastructure;

/// <summary>
/// A refused exchange account or collateral request. Both API surfaces map it to their own error format.
/// </summary>
public sealed class ExchangeOperationException : Exception
{
    public ExchangeOperationException(HttpStatusCode statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public HttpStatusCode StatusCode { get; }
    public string Code { get; }

    public static ExchangeOperationException BadRequest(string code, string message)
    {
        return new ExchangeOperationException(HttpStatusCode.BadRequest, code, message);
    }

    public static ExchangeOperationException NotFound(string code, string message)
    {
        return new ExchangeOperationException(HttpStatusCode.NotFound, code, message);
    }

    public static ExchangeOperationException Conflict(string code, string message)
    {
        return new ExchangeOperationException(HttpStatusCode.Conflict, code, message);
    }
}
