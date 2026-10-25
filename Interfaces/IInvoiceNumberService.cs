namespace Fakturio.Api.Interfaces;

public interface IInvoiceNumberService
{
    Task<string> NextAsync(string? prefix = null, CancellationToken ct = default);
}
