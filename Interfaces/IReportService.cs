using Fakturio.Api.Dtos;

namespace Fakturio.Api.Interfaces;

public interface IReportService
{
    Task<ReportOverviewResponse> GetOverviewAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default);

    Task<MonthlyRevenueResponse> GetMonthlyRevenueAsync(
        int? year,
        CancellationToken ct = default);

    Task<AgingReportResponse> GetAgingAsync(
        int largestLimit = 10,
        CancellationToken ct = default);

    Task<TopCustomersReportResponse> GetTopCustomersAsync(
        DateOnly? from,
        DateOnly? to,
        int limit = 10,
        CancellationToken ct = default);

    Task<VatReportResponse> GetVatSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default);
}
