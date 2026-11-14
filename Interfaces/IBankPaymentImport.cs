using Fakturio.Api.Services.Common;
using Fakturio.Api.Services.Payments;

namespace Fakturio.Api.Interfaces;

/// <summary>Bank feed for payment import (FIO, PSD2 sandbox, …).</summary>
public interface IBankPaymentImport
{
    string ProviderKey { get; }

    Task<ServiceResult<IReadOnlyList<PaymentImportSourceRow>>> FetchTransactionsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);
}