using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IBankDirectoryService
{
    ServiceResult<BankCodeLookupResponse> Lookup(string code, string? country = "SK");
    ServiceResult<BankCodeListResponse> List(string? country = "SK");
    bool TryResolve(string country, string bankCode, out string? name, out string? bic);
}
