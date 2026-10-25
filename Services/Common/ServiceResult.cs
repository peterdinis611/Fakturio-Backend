using Fakturio.Api.Common;

namespace Fakturio.Api.Services.Common;

public class ServiceResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public int StatusCode { get; init; }

    public static ServiceResult Ok(int statusCode = StatusCodes.Status200OK) => new()
    {
        Success = true,
        StatusCode = statusCode
    };

    public static ServiceResult Fail(string error, int statusCode, string? errorCode = null) => new()
    {
        Success = false,
        Error = error,
        ErrorCode = errorCode,
        StatusCode = statusCode
    };

    public static ServiceResult NotFound(string error = "Not found.", string code = ErrorCodes.NotFound) =>
        Fail(error, StatusCodes.Status404NotFound, code);

    public static ServiceResult Conflict(string error, string errorCode = ErrorCodes.Conflict) =>
        Fail(error, StatusCodes.Status409Conflict, errorCode);

    public static ServiceResult BadRequest(string error, string errorCode = ErrorCodes.BadRequest) =>
        Fail(error, StatusCodes.Status400BadRequest, errorCode);
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }

    public static ServiceResult<T> Ok(T data, int statusCode = StatusCodes.Status200OK) => new()
    {
        Success = true,
        Data = data,
        StatusCode = statusCode
    };

    public static ServiceResult<T> Created(T data) => Ok(data, StatusCodes.Status201Created);

    public new static ServiceResult<T> Fail(string error, int statusCode, string? errorCode = null) => new()
    {
        Success = false,
        Error = error,
        ErrorCode = errorCode,
        StatusCode = statusCode
    };

    public new static ServiceResult<T> NotFound(string error = "Not found.", string code = ErrorCodes.NotFound) =>
        Fail(error, StatusCodes.Status404NotFound, code);

    public new static ServiceResult<T> Conflict(string error, string errorCode = ErrorCodes.Conflict) =>
        Fail(error, StatusCodes.Status409Conflict, errorCode);

    public new static ServiceResult<T> BadRequest(string error, string errorCode = ErrorCodes.BadRequest) =>
        Fail(error, StatusCodes.Status400BadRequest, errorCode);
}
