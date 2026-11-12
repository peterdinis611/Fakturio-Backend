using Fakturio.Api.Services.Common;
using Fakturio.Api.Services.Payments;

namespace Fakturio.Api.Interfaces;

public interface IFioBankService
{
    Task<ServiceResult<IReadOnlyList<PaymentImportSourceRow>>> FetchTransactionsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);
}
