using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IAvatarLookupService
{
    ServiceResult<AvatarLookupResponse> FromEmail(string email);
}
