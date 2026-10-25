using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        service = "Fakturio.Api",
        utc = DateTime.UtcNow
    });
}
