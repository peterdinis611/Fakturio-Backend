using Fakturio.Api.Models;

namespace Fakturio.Api.Dtos;

public record ReportPeriodQuery(DateOnly? From = null, DateOnly? To = null);

public record ReportOverviewResponse(
    DateOnly? From,
    DateOnly? To,
    DateOnly AsOf,
    int TotalInvoices,
    int DraftCount,
    int IssuedCount,
    int PaidCount,
    int OverdueCount,
    int CancelledCount,
    decimal IssuedAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    decimal OverdueAmount,
    decimal CancelledAmount,
    decimal AverageInvoiceAmount,
    double? AverageDaysToPay,
    decimal CollectionRatePercent,
    string Currency,
    IReadOnlyList<StatusBreakdownItem> ByStatus
);

public record StatusBreakdownItem(
    InvoiceStatus Status,
    int Count,
    decimal Amount
);

public record MonthlyRevenueResponse(
    int Year,
    string Currency,
    IReadOnlyList<MonthlyRevenueItem> Months,
    decimal YearIssuedTotal,
    decimal YearPaidTotal
);

public record MonthlyRevenueItem(
    int Year,
    int Month,
    string Label,
    int IssuedCount,
    decimal IssuedAmount,
    int PaidCount,
    decimal PaidAmount,
    decimal OutstandingAmount
);

public record AgingReportResponse(
    DateOnly AsOf,
    string Currency,
    decimal TotalOutstanding,
    IReadOnlyList<AgingBucketItem> Buckets,
    IReadOnlyList<AgingInvoiceItem> LargestOutstanding
);

public record AgingBucketItem(
    string Key,
    string Label,
    int FromDaysInclusive,
    int? ToDaysInclusive,
    int Count,
    decimal Amount
);

public record AgingInvoiceItem(
    Guid Id,
    string Number,
    string CustomerName,
    DateOnly DueDate,
    int DaysOverdue,
    decimal Total,
    string Currency,
    InvoiceStatus Status
);

public record TopCustomersReportResponse(
    DateOnly? From,
    DateOnly? To,
    string Currency,
    IReadOnlyList<TopCustomerItem> Customers
);

public record TopCustomerItem(
    Guid CustomerId,
    string CustomerName,
    string? CustomerIco,
    int InvoiceCount,
    int PaidCount,
    int OutstandingCount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    double? AverageDaysToPay,
    decimal ShareOfRevenuePercent
);

public record VatReportResponse(
    DateOnly? From,
    DateOnly? To,
    string Currency,
    decimal TaxBaseTotal,
    decimal VatTotal,
    decimal GrossTotal,
    IReadOnlyList<VatRateBreakdownItem> ByRate
);

public record VatRateBreakdownItem(
    decimal VatRate,
    int ItemCount,
    int InvoiceCount,
    decimal TaxBase,
    decimal VatAmount,
    decimal GrossAmount
);
