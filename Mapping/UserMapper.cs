using Fakturio.Api.Dtos;
using Fakturio.Api.Models;

namespace Fakturio.Api.Mapping;

public static class UserMapper
{
    public static UserResponse ToResponse(AppUser user) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        user.ImageUrl,
        user.CreatedAt,
        user.UpdatedAt,
        user.LastSeenAt);
}
