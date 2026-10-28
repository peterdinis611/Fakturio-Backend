using Fakturio.Api.Dtos;
using Fakturio.Api.Models;

namespace Fakturio.Api.Mapping;

public static class UserMapper
{
    public static UserResponse ToResponse(AppUser user) => new(
        user.Id,
        user.ClerkUserId,
        user.Email,
        user.FirstName,
        user.LastName,
        user.ImageUrl,
        user.CreatedAt,
        user.UpdatedAt,
        user.LastSeenAt);

    public static void Apply(AppUser user, SyncUserRequest request)
    {
        if (request.Email is not null)
            user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (request.FirstName is not null)
            user.FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName.Trim();
        if (request.LastName is not null)
            user.LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();
        if (request.ImageUrl is not null)
            user.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        user.LastSeenAt = DateTime.UtcNow;
    }
}
