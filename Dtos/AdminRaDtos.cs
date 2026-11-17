namespace Fakturio.Api.Dtos;

/// <summary>Flat DTOs for react-admin (simple-rest / Content-Range).</summary>
public record AdminRaUserDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastSeenAt,
    bool IsAdmin,
    string? CompanyName,
    Guid? CompanyId
);

public record AdminRaUpdateUserRequest(
    string? Email,
    string? FirstName,
    string? LastName
);

public record AdminRaCompanyDto(
    Guid Id,
    Guid UserId,
    string? UserEmail,
    string Name,
    string? Ico,
    string? Dic,
    string? IcDph,
    bool IsVatPayer,
    string? Email,
    string? City,
    string Country,
    string? BankIban,
    bool HasFioBankToken,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record AdminRaUpdateCompanyRequest(
    string? Name,
    string? Email,
    string? City,
    string? Country,
    bool? IsVatPayer
);

public record AdminRaInvoiceDto(
    Guid Id,
    Guid CompanyId,
    string? CompanyName,
    string Number,
    string DocumentType,
    string Status,
    string Flow,
    string? CustomerName,
    DateOnly IssueDate,
    DateOnly DueDate,
    string Currency,
    decimal Total,
    DateTime CreatedAt
);

public record AdminRaCustomerDto(
    Guid Id,
    Guid CompanyId,
    string? CompanyName,
    string Name,
    string? Ico,
    string? Email,
    string? City,
    string Country,
    DateTime CreatedAt
);

public record AdminRaAuditLogDto(
    Guid Id,
    Guid? CompanyId,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? EntityNumber,
    string? ActorEmail,
    string? ActorDisplayName,
    string Summary,
    DateTime OccurredAt
);

public record AdminRaNoteDto(
    Guid Id,
    Guid CompanyId,
    string? CompanyName,
    string Title,
    string Body,
    bool IsPinned,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record AdminRaCatalogItemDto(
    Guid Id,
    Guid CompanyId,
    string? CompanyName,
    string Description,
    string Unit,
    decimal UnitPrice,
    decimal VatRate,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record AdminRaRecurringDto(
    Guid Id,
    Guid CompanyId,
    string? CompanyName,
    Guid CustomerId,
    string? CustomerName,
    string Name,
    string Interval,
    int DayOfMonth,
    int DueDays,
    bool IsActive,
    DateOnly NextRunOn,
    DateOnly? LastGeneratedOn,
    Guid? LastInvoiceId,
    int ItemCount,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record AdminRaNotificationDto(
    Guid Id,
    Guid UserId,
    string? UserEmail,
    Guid CompanyId,
    string? CompanyName,
    string Kind,
    string Title,
    string Body,
    string? Href,
    Guid? InvoiceId,
    string? InvoiceNumber,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt
);

public record AdminRaAttachmentDto(
    Guid Id,
    Guid CompanyId,
    string? CompanyName,
    Guid InvoiceId,
    string? InvoiceNumber,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAt
);

/// <summary>Single-record dashboard for react-admin (Id is always "current").</summary>
public record AdminRaDashboardDto(
    string Id,
    int Users,
    int Companies,
    int Customers,
    int Invoices,
    int RecurringTemplates,
    int Notes,
    int CatalogItems,
    int Notifications,
    int Attachments,
    int AuditLogs,
    int DraftInvoices,
    int IssuedInvoices,
    int PaidInvoices,
    int OverdueInvoices,
    int CancelledInvoices,
    decimal OpenAmount,
    decimal PaidAmount,
    string Currency,
    DateTime GeneratedAtUtc
);

/// <summary>Job settings wrapped for react-admin getOne/update (Id is always "current").</summary>
public record AdminRaJobSettingsDto(
    string Id,
    JobSettingsResponse Settings
);
