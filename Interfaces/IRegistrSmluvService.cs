using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IRegistrSmluvService
{
    Task<ServiceResult<RegistrSmluvStatusResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default);
}
