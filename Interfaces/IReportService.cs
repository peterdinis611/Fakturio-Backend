using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IReportService
{
    Task<ServiceResult<ReportOverviewResponse>> GetOverviewAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default);

    Task<ServiceResult<MonthlyRevenueResponse>> GetMonthlyRevenueAsync(
        int? year,
        CancellationToken ct = default);

    Task<ServiceResult<AgingReportResponse>> GetAgingAsync(
        int largestLimit = 10,
        CancellationToken ct = default);

    Task<ServiceResult<TopCustomersReportResponse>> GetTopCustomersAsync(
        DateOnly? from,
        DateOnly? to,
        int limit = 10,
        CancellationToken ct = default);

    Task<ServiceResult<VatReportResponse>> GetVatSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default);
}
