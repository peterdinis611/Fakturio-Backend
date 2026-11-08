using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IRegisterUzService
{
    Task<ServiceResult<RegisterUzUnitResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default);
}
