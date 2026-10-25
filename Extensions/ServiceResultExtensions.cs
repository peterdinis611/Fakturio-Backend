using Fakturio.Api.Common;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Extensions;

public static class ServiceResultExtensions
{
    public static ActionResult ToActionResult(this ServiceResult result, ControllerBase controller)
    {
        if (result.Success)
        {
            return result.StatusCode switch
            {
                StatusCodes.Status204NoContent => controller.NoContent(),
                _ => controller.StatusCode(result.StatusCode)
            };
        }

        return Problem(controller, result.StatusCode, result.Error, result.ErrorCode);
    }

    public static ActionResult<T> ToActionResult<T>(
        this ServiceResult<T> result,
        ControllerBase controller,
        string? createdAtActionName = null,
        Func<T, object>? createdRouteValues = null)
    {
        if (!result.Success)
            return Problem(controller, result.StatusCode, result.Error, result.ErrorCode);

        if (result.StatusCode == StatusCodes.Status201Created
            && createdAtActionName is not null
            && result.Data is not null
            && createdRouteValues is not null)
        {
            return controller.CreatedAtAction(
                createdAtActionName,
                createdRouteValues(result.Data),
                result.Data);
        }

        return controller.StatusCode(result.StatusCode, result.Data);
    }

    public static ActionResult ToFileResult(
        this ServiceResult<InvoicePdfFile> result,
        ControllerBase controller)
    {
        if (!result.Success || result.Data is null)
            return Problem(controller, result.StatusCode, result.Error, result.ErrorCode);

        return controller.File(
            result.Data.Content,
            result.Data.ContentType,
            result.Data.FileName);
    }

    private static ObjectResult Problem(
        ControllerBase controller,
        int statusCode,
        string? detail,
        string? code)
    {
        return controller.StatusCode(
            statusCode,
            ApiProblemDetails.From(
                statusCode,
                detail ?? "Request failed.",
                code,
                controller.HttpContext.Request.Path));
    }
}
