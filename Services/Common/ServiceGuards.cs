using Fakturio.Api.Common;

namespace Fakturio.Api.Services.Common;

public static class ServiceGuards
{
    public const int DefaultMaxPageSize = 100;
    public const int DefaultMinSearchLength = 2;
    public const int DefaultMaxSearchLength = 100;
    public const int MaxDateRangeDays = 366 * 5;

    public static (int Page, int PageSize) NormalizePaging(
        int page,
        int pageSize,
        int maxPageSize = DefaultMaxPageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, maxPageSize);
        return (page, pageSize);
    }

    public static ServiceResult? ValidateGuid(Guid id, string entityName, string code = ErrorCodes.BadRequest)
    {
        if (id == Guid.Empty)
            return ServiceResult.BadRequest($"{entityName} id is required.", code);
        return null;
    }

    public static ServiceResult? ValidateDateRange(
        DateOnly? from,
        DateOnly? to,
        int maxDays = MaxDateRangeDays)
    {
        if (from.HasValue && to.HasValue && from > to)
            return ServiceResult.BadRequest(
                "Date range is invalid: 'from' cannot be after 'to'.",
                ErrorCodes.InvalidDateRange);

        if (from.HasValue && to.HasValue)
        {
            var days = to.Value.DayNumber - from.Value.DayNumber;
            if (days > maxDays)
                return ServiceResult.BadRequest(
                    $"Date range cannot exceed {maxDays} days.",
                    ErrorCodes.DateRangeTooLarge);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (from.HasValue && from.Value > today.AddYears(1))
            return ServiceResult.BadRequest(
                "Start date is too far in the future.",
                ErrorCodes.InvalidDateRange);

        return null;
    }

    public static ServiceResult? ValidateInvoiceDates(
        DateOnly issueDate,
        DateOnly dueDate,
        DateOnly? deliveryDate)
    {
        if (dueDate < issueDate)
            return ServiceResult.BadRequest(
                "Due date cannot be before issue date.",
                ErrorCodes.InvalidDateRange);

        if (deliveryDate.HasValue && deliveryDate.Value > issueDate)
            return ServiceResult.BadRequest(
                "Delivery date cannot be after issue date.",
                ErrorCodes.InvalidDateRange);

        var min = new DateOnly(2000, 1, 1);
        var max = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(2);
        if (issueDate < min || issueDate > max)
            return ServiceResult.BadRequest(
                $"Issue date must be between {min:yyyy-MM-dd} and {max:yyyy-MM-dd}.",
                ErrorCodes.InvalidDateRange);

        return null;
    }

    public static ServiceResult? ValidateSearchTerm(
        string? term,
        bool required = true,
        int minLength = DefaultMinSearchLength,
        int maxLength = DefaultMaxSearchLength)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return required
                ? ServiceResult.BadRequest("Search query is required.", ErrorCodes.InvalidSearchQuery)
                : null;
        }

        var trimmed = term.Trim();
        if (trimmed.Length < minLength)
            return ServiceResult.BadRequest(
                $"Search query must be at least {minLength} characters.",
                ErrorCodes.InvalidSearchQuery);

        if (trimmed.Length > maxLength)
            return ServiceResult.BadRequest(
                $"Search query cannot exceed {maxLength} characters.",
                ErrorCodes.InvalidSearchQuery);

        return null;
    }

    public static string? NormalizeOptionalSearch(string? term, int minLength = DefaultMinSearchLength)
    {
        if (string.IsNullOrWhiteSpace(term))
            return null;

        var trimmed = term.Trim();
        return trimmed.Length < minLength ? null : trimmed;
    }

    public static ServiceResult? ValidateYear(int year)
    {
        var current = DateTime.UtcNow.Year;
        if (year < 2000 || year > current + 1)
            return ServiceResult.BadRequest(
                $"Year must be between 2000 and {current + 1}.",
                ErrorCodes.InvalidDateRange);
        return null;
    }

    public static ServiceResult? EnsurePositiveTotal(decimal total)
    {
        if (total <= 0)
            return ServiceResult.BadRequest(
                "Invoice total must be greater than zero.",
                ErrorCodes.InvalidInvoiceTotal);
        return null;
    }

    public static ServiceResult? EnsureHasItems(int count)
    {
        if (count <= 0)
            return ServiceResult.BadRequest(
                "At least one invoice item is required.",
                ErrorCodes.InvalidInvoiceItems);
        if (count > 200)
            return ServiceResult.BadRequest(
                "Maximum 200 items per invoice.",
                ErrorCodes.InvalidInvoiceItems);
        return null;
    }
}
