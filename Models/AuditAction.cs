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
    ProformaFinalized = 10
}
