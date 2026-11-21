namespace Fakturio.Api.Caching;

public interface ICacheInvalidator
{
    Task InvalidateReportsAsync(Guid companyId, CancellationToken ct = default);
    Task InvalidateCompanyAsync(Guid companyId, CancellationToken ct = default);
    /// <summary>Admin/GDPR flush — clears legacy global tags and every known company tag.</summary>
    Task InvalidateAllAsync(CancellationToken ct = default);
}
