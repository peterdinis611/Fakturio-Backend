using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IAdminRaService
{
    Task<(IReadOnlyList<AdminRaUserDto> Items, int Total)> ListUsersAsync(
        int skip, int take, string? sort, string? order, string? q, CancellationToken ct = default);

    Task<ServiceResult<AdminRaUserDto>> GetUserAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<AdminRaUserDto>> UpdateUserAsync(
        Guid id, AdminRaUpdateUserRequest request, CancellationToken ct = default);

    Task<ServiceResult> DeleteUserAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> RevokeUserSessionsAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminRaCompanyDto> Items, int Total)> ListCompaniesAsync(
        int skip, int take, string? sort, string? order, string? q, CancellationToken ct = default);

    Task<ServiceResult<AdminRaCompanyDto>> GetCompanyAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<AdminRaCompanyDto>> UpdateCompanyAsync(
        Guid id, AdminRaUpdateCompanyRequest request, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminRaInvoiceDto> Items, int Total)> ListInvoicesAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, string? status = null, CancellationToken ct = default);

    Task<ServiceResult<AdminRaInvoiceDto>> GetInvoiceAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> DeleteInvoiceAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminRaCustomerDto> Items, int Total)> ListCustomersAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, CancellationToken ct = default);

    Task<ServiceResult<AdminRaCustomerDto>> GetCustomerAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminRaAuditLogDto> Items, int Total)> ListAuditLogsAsync(
        int skip, int take, string? sort, string? order, string? q, CancellationToken ct = default);

    Task<ServiceResult<AdminRaAuditLogDto>> GetAuditLogAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminRaNoteDto> Items, int Total)> ListNotesAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, CancellationToken ct = default);

    Task<ServiceResult<AdminRaNoteDto>> GetNoteAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> DeleteNoteAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminRaCatalogItemDto> Items, int Total)> ListCatalogItemsAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, CancellationToken ct = default);

    Task<ServiceResult<AdminRaCatalogItemDto>> GetCatalogItemAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> DeleteCatalogItemAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminRaRecurringDto> Items, int Total)> ListRecurringAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, CancellationToken ct = default);

    Task<ServiceResult<AdminRaRecurringDto>> GetRecurringAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> DeleteRecurringAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminRaNotificationDto> Items, int Total)> ListNotificationsAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, CancellationToken ct = default);

    Task<ServiceResult<AdminRaNotificationDto>> GetNotificationAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> DeleteNotificationAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminRaAttachmentDto> Items, int Total)> ListAttachmentsAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, CancellationToken ct = default);

    Task<ServiceResult<AdminRaAttachmentDto>> GetAttachmentAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<AdminRaDashboardDto>> GetDashboardAsync(CancellationToken ct = default);

    Task<ServiceResult<AdminRaJobSettingsDto>> GetJobSettingsAsync(CancellationToken ct = default);

    Task<ServiceResult<AdminRaJobSettingsDto>> UpdateJobSettingsAsync(
        AdminRaJobSettingsDto request, CancellationToken ct = default);
}
