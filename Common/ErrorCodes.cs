namespace Fakturio.Api.Common;

public static class ErrorCodes
{
    public const string NotFound = "not_found";
    public const string BadRequest = "bad_request";
    public const string Conflict = "conflict";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
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
    public const string InvalidDocumentType = "invalid_document_type";
    public const string UseCancelEndpoint = "use_cancel_endpoint";
    public const string InvalidDateRange = "invalid_date_range";
    public const string DateRangeTooLarge = "date_range_too_large";
    public const string InvalidSearchQuery = "invalid_search_query";
    public const string InvalidInvoiceTotal = "invalid_invoice_total";
    public const string InvalidInvoiceItems = "invalid_invoice_items";
    public const string DuplicateEmail = "duplicate_email";
    public const string EmptyGuid = "empty_guid";

    public const string InvalidIco = "invalid_ico";
    public const string UnsupportedCountry = "unsupported_country";
    public const string RegistryNotFound = "registry_not_found";
    public const string RegistryUnavailable = "registry_unavailable";
    public const string InvalidVatId = "invalid_vat_id";
    public const string ViesUnavailable = "vies_unavailable";
    public const string FxUnavailable = "fx_unavailable";
    public const string InvalidIban = "invalid_iban";
    public const string FsUnavailable = "fs_unavailable";
    public const string AddressUnavailable = "address_unavailable";
    public const string OrsrUnavailable = "orsr_unavailable";
    public const string AdisregUnavailable = "adisreg_unavailable";
    public const string IsirUnavailable = "isir_unavailable";
    public const string RegisterUzUnavailable = "register_uz_unavailable";
    public const string InvalidPostalCode = "invalid_postal_code";
    public const string PostalUnavailable = "postal_unavailable";
    public const string AresVrUnavailable = "ares_vr_unavailable";
    public const string BusinessDayUnavailable = "business_day_unavailable";
    public const string RpvsUnavailable = "rpvs_unavailable";
    public const string RegistrSmluvUnavailable = "registr_smluv_unavailable";
    public const string LeiUnavailable = "lei_unavailable";

    public const string FileTooLarge = "file_too_large";
    public const string InvalidFileType = "invalid_file_type";
    public const string CompanyNotReady = "company_not_ready";
}
