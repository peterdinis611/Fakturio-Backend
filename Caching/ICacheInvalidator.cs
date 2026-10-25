namespace Fakturio.Api.Caching;

public interface ICacheInvalidator
{
    Task InvalidateReportsAsync(CancellationToken ct = default);
    Task InvalidateCompanyAsync(CancellationToken ct = default);
    Task InvalidateAllAsync(CancellationToken ct = default);
}
