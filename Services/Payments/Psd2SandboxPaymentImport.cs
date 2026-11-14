using Fakturio.Api.Common;
using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Payments;

/// <summary>PSD2 sandbox payment import placeholder (disabled by default).</summary>
public sealed class Psd2SandboxPaymentImport(IOptions<BankingOptions> options) : IBankPaymentImport
{
    public string ProviderKey => "psd2-sandbox";

    public Task<ServiceResult<IReadOnlyList<PaymentImportSourceRow>>> FetchTransactionsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        if (!options.Value.Psd2Sandbox.Enabled)
        {
            return Task.FromResult(ServiceResult<IReadOnlyList<PaymentImportSourceRow>>.BadRequest(
                "PSD2 sandbox import is not enabled.",
                ErrorCodes.BadRequest));
        }

        return Task.FromResult(ServiceResult<IReadOnlyList<PaymentImportSourceRow>>.Ok(
            Array.Empty<PaymentImportSourceRow>()));
    }
}