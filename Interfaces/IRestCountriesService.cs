using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IRestCountriesService
{
    Task<ServiceResult<CountriesListResponse>> GetAllAsync(CancellationToken ct = default);
}