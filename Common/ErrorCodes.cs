namespace Fakturio.Api.Common;

public static class ErrorCodes
{
    public const string NotFound = "not_found";
    public const string BadRequest = "bad_request";
    public const string Conflict = "conflict";
    public const string Validation = "validation_failed";

    public const string CustomerNotFound = "customer_not_found";
    public const string DuplicateIco = "duplicate_ico";
    public const string HasInvoices = "has_invoices";

    public const string InvoiceNotFound = "invoice_not_found";
    public const string NotDraft = "not_draft";
    public const string Draft = "draft";
    public const string Cancelled = "cancelled";
    public const string AlreadyCancelled = "already_cancelled";
    public const string Paid = "paid";
    public const string PaidToDraft = "paid_to_draft";
    public const string InvalidPaidAt = "invalid_paid_at";
    public const string InvalidStatus = "invalid_status";
    public const string UseCancelEndpoint = "use_cancel_endpoint";
}
