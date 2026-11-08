using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IBusinessDayService
{
    ServiceResult<BusinessDayResponse> AddBusinessDays(
        DateOnly from,
        int addDays,
        string? country = "SK");
}
