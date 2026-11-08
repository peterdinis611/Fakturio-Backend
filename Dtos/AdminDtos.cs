namespace Fakturio.Api.Dtos;

public record AdminOverviewResponse(
    AdminHealthDto Health,
    AdminCountsDto Counts,
    AdminInvoicesDto Invoices,
    IReadOnlyList<AdminUserSummaryDto> RecentUsers,
    JobSettingsResponse Jobs,
    DateTime GeneratedAtUtc
);

public record AdminHealthDto(
    string Status,
    string Database,
    DateTime Utc
);

public record AdminCountsDto(
    int Users,
    int Companies,
    int Customers,
    int Invoices,
    int RecurringTemplates,
    int Notes,
    int CatalogItems,
    int AuditLogs
);

public record AdminInvoicesDto(
    int Draft,
    int Issued,
    int Paid,
    int Overdue,
    int Cancelled,
    decimal OpenAmount,
    decimal PaidAmount,
    string Currency
);

public record AdminUserSummaryDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    DateTime CreatedAt,
    DateTime? LastSeenAt,
    bool IsAdmin,
    string? CompanyName
);
