namespace Fakturio.Api.Interfaces;

public interface ITenantContext
{
    /// <summary>Current authenticated user id (throws if missing).</summary>
    Task<Guid> GetUserIdAsync(CancellationToken ct = default);

    /// <summary>Current user's company id; creates empty company row if needed.</summary>
    Task<Guid> GetCompanyIdAsync(CancellationToken ct = default);

    Task<(Guid UserId, Guid CompanyId)> GetAsync(CancellationToken ct = default);
}
