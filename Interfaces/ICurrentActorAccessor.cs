namespace Fakturio.Api.Interfaces;

public record AuditActor(
    Guid? UserId,
    string? Subject,
    string? DisplayName,
    string? Email);

public interface ICurrentActorAccessor
{
    Task<AuditActor> GetAsync(CancellationToken ct = default);
}
