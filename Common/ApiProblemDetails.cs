using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Common;

public sealed class ApiProblemDetails : ProblemDetails
{
    public string? Code { get; set; }

    public static ApiProblemDetails From(
        int statusCode,
        string detail,
        string? code = null,
        string? instance = null)
    {
        return new ApiProblemDetails
        {
            Status = statusCode,
            Title = TitleFor(statusCode),
            Detail = detail,
            Code = code,
            Instance = instance,
            Type = $"https://httpstatuses.com/{statusCode}"
        };
    }

    private static string TitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
        _ => "Error"
    };
}
