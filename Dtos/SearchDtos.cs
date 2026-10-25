using Fakturio.Api.Models;

namespace Fakturio.Api.Dtos;

[Flags]
public enum SearchEntityType
{
    None = 0,
    Customers = 1,
    Invoices = 2,
    All = Customers | Invoices
}

public record SearchRequest(
    string Query,
    SearchEntityType? Types = null,
    int Limit = 20,
    InvoiceStatus? InvoiceStatus = null
);

public record SearchHitResponse(
    string Type,
    Guid Id,
    string Title,
    string? Subtitle,
    string? Number,
    string? Email,
    string? Ico,
    InvoiceStatus? Status,
    decimal? Amount,
    string? Currency,
    DateOnly? IssueDate,
    double Score
);

public record GlobalSearchResponse(
    string Query,
    SearchEntityType Types,
    int Limit,
    int TotalHits,
    IReadOnlyList<SearchHitResponse> Hits
);

public record EntitySearchResponse<T>(
    string Query,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<T> Items
);
