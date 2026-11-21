using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IMfEinvoiceService
{
    Task<ServiceResult<MfEinvoiceLookupResponse>> SearchByIcoAsync(string ico, CancellationToken ct = default);
}
