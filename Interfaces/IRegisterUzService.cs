using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IRegisterUzService
{
    Task<ServiceResult<RegisterUzUnitResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default);

    Task<ServiceResult<RegisterUzStatementsResponse>> LookupStatementsByIcoAsync(
        string ico,
        int take = 5,
        CancellationToken ct = default);
}
