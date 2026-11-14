using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface ISkObchodnyVestnikService
{
    Task<ServiceResult<SkObchodnyVestnikResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default);
}