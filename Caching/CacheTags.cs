namespace Fakturio.Api.Caching;

public static class CacheTags
{
    /// <summary>Legacy global tag — kept for InvalidateAllAsync / rollout compat.</summary>
    public const string ReportsGlobal = "reports";

    /// <summary>Legacy global tag — kept for InvalidateAllAsync / rollout compat.</summary>
    public const string CompanyGlobal = "company";

    public static string Reports(Guid companyId) => $"reports:{companyId:N}";

    public static string Company(Guid companyId) => $"company:{companyId:N}";
}
