namespace Fakturio.Api.Dtos;

public record ResumeProfileResponse(
    Guid Id,
    string FullName,
    string Headline,
    string Summary,
    string Email,
    string Phone,
    string Location,
    string Website,
    string Skills,
    DateTime UpdatedAt
);

public record UpdateResumeProfileRequest(
    string? FullName,
    string? Headline,
    string? Summary,
    string? Email,
    string? Phone,
    string? Location,
    string? Website,
    string? Skills
);

public record ResumeEntryResponse(
    Guid Id,
    string Type,
    string Title,
    string Organization,
    string Location,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    string Description,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateResumeEntryRequest(
    string Type,
    string Title,
    string? Organization,
    string? Location,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool? IsCurrent,
    string? Description,
    int? SortOrder
);

public record UpdateResumeEntryRequest(
    string? Type,
    string? Title,
    string? Organization,
    string? Location,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool? IsCurrent,
    string? Description,
    int? SortOrder
);
