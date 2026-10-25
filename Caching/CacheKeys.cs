namespace Fakturio.Api.Caching;

public static class CacheKeys
{
    public static string ReportOverview(DateOnly? from, DateOnly? to, DateOnly asOf) =>
        $"reports:overview:{Format(from)}:{Format(to)}:{asOf:yyyyMMdd}";

    public static string ReportMonthly(int year) =>
        $"reports:monthly:{year}";

    public static string ReportAging(DateOnly asOf, int largestLimit) =>
        $"reports:aging:{asOf:yyyyMMdd}:{largestLimit}";

    public static string ReportTopCustomers(DateOnly? from, DateOnly? to, int limit) =>
        $"reports:customers-top:{Format(from)}:{Format(to)}:{limit}";

    public static string ReportVat(DateOnly? from, DateOnly? to) =>
        $"reports:vat:{Format(from)}:{Format(to)}";

    public const string CompanySettings = "company:settings";
    public const string CompanySummary = "company:summary";
    public const string CompanyStatus = "company:status";

    private static string Format(DateOnly? value) =>
        value?.ToString("yyyyMMdd") ?? "_";
}
