using Fakturio.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Authorize]
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status409Conflict)]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<T> OkOrNotFound<T>(T? value, string detail, string code = ErrorCodes.NotFound)
        where T : class
    {
        if (value is not null)
            return Ok(value);

        return NotFound(ApiProblemDetails.From(
            StatusCodes.Status404NotFound,
            detail,
            code,
            HttpContext.Request.Path));
    }
}
