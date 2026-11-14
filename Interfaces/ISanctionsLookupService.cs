using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface ISanctionsLookupService
{
    Task<ServiceResult<SanctionsSearchResponse>> SearchAsync(
        string query,
        CancellationToken ct = default);
}