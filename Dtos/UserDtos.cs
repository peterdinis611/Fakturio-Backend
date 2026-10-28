namespace Fakturio.Api.Dtos;

public record UserResponse(
    Guid Id,
    string ClerkUserId,
    string? Email,
    string? FirstName,
    string? LastName,
    string? ImageUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastSeenAt
);

public record SyncUserRequest(
    string? Email,
    string? FirstName,
    string? LastName,
    string? ImageUrl
);
