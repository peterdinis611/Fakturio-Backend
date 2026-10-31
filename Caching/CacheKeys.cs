namespace Fakturio.Api.Caching;

public static class CacheKeys
{
    public static string ReportOverview(Guid companyId, DateOnly? from, DateOnly? to, DateOnly asOf) =>
        $"reports:{companyId:N}:overview:{Format(from)}:{Format(to)}:{asOf:yyyyMMdd}";

    public static string ReportMonthly(Guid companyId, int year) =>
        $"reports:{companyId:N}:monthly:{year}";

    public static string ReportAging(Guid companyId, DateOnly asOf, int largestLimit) =>
        $"reports:{companyId:N}:aging:{asOf:yyyyMMdd}:{largestLimit}";

    public static string ReportTopCustomers(Guid companyId, DateOnly? from, DateOnly? to, int limit) =>
        $"reports:{companyId:N}:customers-top:{Format(from)}:{Format(to)}:{limit}";

    public static string ReportVat(Guid companyId, DateOnly? from, DateOnly? to) =>
        $"reports:{companyId:N}:vat:{Format(from)}:{Format(to)}";

    public static string CompanySettings(Guid companyId) => $"company:{companyId:N}:settings";
    public static string CompanySummary(Guid companyId) => $"company:{companyId:N}:summary";
    public static string CompanyStatus(Guid companyId) => $"company:{companyId:N}:status";
    public static string CompanyBank(Guid companyId) => $"company:{companyId:N}:bank";

    private static string Format(DateOnly? value) =>
        value?.ToString("yyyyMMdd") ?? "_";
}
