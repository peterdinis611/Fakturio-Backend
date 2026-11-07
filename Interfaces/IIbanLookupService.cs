using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IIbanLookupService
{
    Task<ServiceResult<IbanLookupResponse>> LookupAsync(
        string iban,
        CancellationToken ct = default);
}
