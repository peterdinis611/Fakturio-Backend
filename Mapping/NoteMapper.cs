using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Notes;

namespace Fakturio.Api.Mapping;

public static class NoteMapper
{
    public static NoteResponse ToResponse(Note note) =>
        new(
            note.Id,
            note.Title,
            note.Body,
            note.IsPinned,
            note.CreatedAt,
            note.UpdatedAt);

    public static Note FromCreate(CreateNoteRequest request) =>
        new()
        {
            Title = request.Title.Trim(),
            Body = NoteHtmlSanitizer.Sanitize(request.Body),
            IsPinned = request.IsPinned ?? false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    public static void ApplyUpdate(Note note, UpdateNoteRequest request)
    {
        if (request.Title is not null)
            note.Title = request.Title.Trim();
        if (request.Body is not null)
            note.Body = NoteHtmlSanitizer.Sanitize(request.Body);
        if (request.IsPinned.HasValue)
            note.IsPinned = request.IsPinned.Value;
        note.UpdatedAt = DateTime.UtcNow;
    }
}
