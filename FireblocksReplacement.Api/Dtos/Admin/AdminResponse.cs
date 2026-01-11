namespace FireblocksReplacement.Api.Dtos.Admin;

public class AdminResponse<T>
{
    public T? Data { get; set; }
    public AdminError? Error { get; set; }

    public static AdminResponse<T> Success(T data)
    {
        return new AdminResponse<T>
        {
            Data = data,
            Error = null
        };
    }

    public static AdminResponse<T> Failure(string message, string code)
    {
        return new AdminResponse<T>
        {
            Data = default,
            Error = new AdminError
            {
                Message = message,
                Code = code
            }
        };
    }
}

public class AdminError
{
    public string Message { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
