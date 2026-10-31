using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IGdprService
{
    Task<ServiceResult<ExportFile>> ExportMyDataAsync(CancellationToken ct = default);

    Task<ServiceResult> DeleteMyAccountAsync(
        DeleteAccountRequest request,
        CancellationToken ct = default);
}
