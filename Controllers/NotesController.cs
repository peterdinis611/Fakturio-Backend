using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/notes")]
public sealed class NotesController(INoteService notes) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<NoteResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<NoteResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] bool? pinnedOnly = null,
        CancellationToken ct = default)
        => Ok(await notes.ListAsync(page, pageSize, search, pinnedOnly, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NoteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NoteResponse>> Get(Guid id, CancellationToken ct)
        => OkOrNotFound(
            await notes.GetAsync(id, ct),
            "Poznámka nebola nájdená.",
            ErrorCodes.NotFound);

    [HttpPost]
    [ProducesResponseType(typeof(NoteResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<NoteResponse>> Create(
        [FromBody] CreateNoteRequest request,
        CancellationToken ct)
    {
        var result = await notes.CreateAsync(request, ct);
        return result.ToActionResult(this, nameof(Get), n => new { id = n.Id });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(NoteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NoteResponse>> Update(
        Guid id,
        [FromBody] UpdateNoteRequest request,
        CancellationToken ct)
        => (await notes.UpdateAsync(id, request, ct)).ToActionResult(this);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
        => (await notes.DeleteAsync(id, ct)).ToActionResult(this);
}
