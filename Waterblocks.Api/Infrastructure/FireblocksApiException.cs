using System.Net;

namespace Waterblocks.Api.Infrastructure;

public sealed class FireblocksApiException : Exception
{
    public FireblocksApiException(HttpStatusCode statusCode, decimal errorCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    public HttpStatusCode StatusCode { get; }
    public decimal ErrorCode { get; }

    public static FireblocksApiException BadRequest(string message, decimal errorCode = 400)
    {
        return new FireblocksApiException(HttpStatusCode.BadRequest, errorCode, message);
    }

    public static FireblocksApiException Unauthorized(string message = "Unauthorized", decimal errorCode = 401)
    {
        return new FireblocksApiException(HttpStatusCode.Unauthorized, errorCode, message);
    }

    public static FireblocksApiException NotFound(string message, decimal errorCode = 404)
    {
        return new FireblocksApiException(HttpStatusCode.NotFound, errorCode, message);
    }
}
