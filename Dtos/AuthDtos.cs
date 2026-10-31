namespace Fakturio.Api.Dtos;

public record RegisterRequest(
    string Email,
    string Password,
    string? FirstName,
    string? LastName
);

public record LoginRequest(
    string Email,
    string Password
);

public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    UserResponse User
);

public record UserResponse(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string? ImageUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastSeenAt
);

public record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? ImageUrl,
    string? Email
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);
