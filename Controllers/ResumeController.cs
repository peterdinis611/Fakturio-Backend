using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/resume")]
public sealed class ResumeController(IResumeService resume) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ResumeProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResumeProfileResponse>> GetProfile(CancellationToken ct)
        => Ok(await resume.GetProfileAsync(ct));

    [HttpPut]
    [ProducesResponseType(typeof(ResumeProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResumeProfileResponse>> UpdateProfile(
        [FromBody] UpdateResumeProfileRequest request,
        CancellationToken ct)
        => (await resume.UpdateProfileAsync(request, ct)).ToActionResult(this);

    [HttpGet("entries")]
    [ProducesResponseType(typeof(PagedResponse<ResumeEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ResumeEntryResponse>>> ListEntries(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? type = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
        => Ok(await resume.ListEntriesAsync(page, pageSize, type, search, ct));

    [HttpGet("entries/{id:guid}")]
    [ProducesResponseType(typeof(ResumeEntryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResumeEntryResponse>> GetEntry(Guid id, CancellationToken ct)
        => OkOrNotFound(
            await resume.GetEntryAsync(id, ct),
            "Resume entry not found.",
            ErrorCodes.NotFound);

    [HttpPost("entries")]
    [ProducesResponseType(typeof(ResumeEntryResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ResumeEntryResponse>> CreateEntry(
        [FromBody] CreateResumeEntryRequest request,
        CancellationToken ct)
    {
        var result = await resume.CreateEntryAsync(request, ct);
        return result.ToActionResult(this, nameof(GetEntry), e => new { id = e.Id });
    }

    [HttpPut("entries/{id:guid}")]
    [ProducesResponseType(typeof(ResumeEntryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResumeEntryResponse>> UpdateEntry(
        Guid id,
        [FromBody] UpdateResumeEntryRequest request,
        CancellationToken ct)
        => (await resume.UpdateEntryAsync(id, request, ct)).ToActionResult(this);

    [HttpDelete("entries/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteEntry(Guid id, CancellationToken ct)
        => (await resume.DeleteEntryAsync(id, ct)).ToActionResult(this);
}
