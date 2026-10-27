using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/jobs")]
public sealed class JobsController(IJobSettingsStore store) : ApiControllerBase
{
    [HttpGet("settings")]
    [ProducesResponseType(typeof(JobSettingsResponse), StatusCodes.Status200OK)]
    public ActionResult<JobSettingsResponse> GetSettings()
        => Ok(store.GetSnapshot());

    [HttpPut("settings")]
    [ProducesResponseType(typeof(JobSettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<JobSettingsResponse>> UpdateSettings(
        [FromBody] UpdateJobSettingsRequest request,
        CancellationToken ct)
        => Ok(await store.UpdateAsync(request, ct));
}
