namespace Fakturio.Api.Models;

public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Issued = 2,
    Paid = 3,
    Cancelled = 4,
    ReminderSent = 5,
    Deleted = 6,
    Duplicated = 7,
    OverdueMarked = 8,
    CreditNoteCreated = 9,
    ProformaFinalized = 10,
    CustomerCreated = 11,
    CustomerUpdated = 12,
    CustomerDeleted = 13,
    CompanyUpdated = 14,
    PaymentImportApplied = 15,
    ExportRequested = 16,
    GdprExport = 17,
    GdprErasure = 18,
    RecurringCreated = 19,
    RecurringUpdated = 20,
    RecurringDeleted = 21,
    RecurringGenerated = 22,
    LoginSucceeded = 23,
}
