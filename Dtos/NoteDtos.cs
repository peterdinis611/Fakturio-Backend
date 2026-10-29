namespace Fakturio.Api.Dtos;

public record NoteResponse(
    Guid Id,
    string Title,
    string Body,
    bool IsPinned,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateNoteRequest(
    string Title,
    string? Body,
    bool? IsPinned
);

public record UpdateNoteRequest(
    string? Title,
    string? Body,
    bool? IsPinned
);
