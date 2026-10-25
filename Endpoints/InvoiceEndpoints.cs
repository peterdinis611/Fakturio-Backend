using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Endpoints;

public static class InvoiceEndpoints
{
    public static RouteGroupBuilder MapInvoiceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/invoices").WithTags("Invoices");

        group.MapGet("/", ListInvoices);
        group.MapGet("/{id:guid}", GetInvoice);
        group.MapPost("/", CreateInvoice);
        group.MapPatch("/{id:guid}/status", UpdateStatus);
        group.MapDelete("/{id:guid}", DeleteInvoice);

        return group;
    }

    private static async Task<IResult> ListInvoices(
        AppDbContext db,
        InvoiceStatus? status,
        Guid? customerId,
        CancellationToken ct)
    {
        var query = db.Invoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        if (customerId.HasValue)
            query = query.Where(i => i.CustomerId == customerId.Value);

        var invoices = await query
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.CreatedAt)
            .Select(i => new InvoiceListItemResponse(
                i.Id,
                i.Number,
                i.Status,
                i.CustomerId,
                i.Customer.Name,
                i.IssueDate,
                i.DueDate,
                i.Currency,
                i.Total,
                i.CreatedAt
            ))
            .ToListAsync(ct);

        return Results.Ok(invoices);
    }

    private static async Task<IResult> GetInvoice(Guid id, AppDbContext db, CancellationToken ct)
    {
        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        return invoice is null ? Results.NotFound() : Results.Ok(ToResponse(invoice));
    }

    private static async Task<IResult> CreateInvoice(
        CreateInvoiceRequest request,
        AppDbContext db,
        InvoiceNumberService numbers,
        CancellationToken ct)
    {
        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest(new { error = "At least one invoice item is required." });

        var customer = await db.Customers.FindAsync([request.CustomerId], ct);
        if (customer is null)
            return Results.BadRequest(new { error = "Customer not found." });

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
                return Results.BadRequest(new { error = "Item description is required." });
            if (item.Quantity <= 0)
                return Results.BadRequest(new { error = "Item quantity must be greater than zero." });
            if (item.UnitPrice < 0)
                return Results.BadRequest(new { error = "Item unit price cannot be negative." });
        }

        var issueDate = request.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDate = request.DueDate ?? issueDate.AddDays(14);
        var number = await numbers.NextAsync(ct);
        var status = request.Status ?? InvoiceStatus.Issued;

        var invoice = new Invoice
        {
            Number = number,
            Status = status,
            CustomerId = request.CustomerId,
            IssueDate = issueDate,
            DueDate = dueDate,
            DeliveryDate = request.DeliveryDate,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency.Trim().ToUpperInvariant(),
            VariableSymbol = request.VariableSymbol ?? number,
            ConstantSymbol = request.ConstantSymbol,
            SpecificSymbol = request.SpecificSymbol,
            PaymentMethod = request.PaymentMethod ?? "bank_transfer",
            BankIban = request.BankIban,
            BankBic = request.BankBic,
            Notes = request.Notes,
            PaidAt = status == InvoiceStatus.Paid ? issueDate : null
        };

        var order = 0;
        foreach (var item in request.Items)
        {
            invoice.Items.Add(new InvoiceItem
            {
                Description = item.Description.Trim(),
                Unit = string.IsNullOrWhiteSpace(item.Unit) ? "ks" : item.Unit.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                VatRate = item.VatRate ?? 23m,
                SortOrder = order++
            });
        }

        InvoiceCalculator.RecalculateTotals(invoice);

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        await db.Entry(invoice).Reference(i => i.Customer).LoadAsync(ct);

        return Results.Created($"/api/invoices/{invoice.Id}", ToResponse(invoice));
    }

    private static async Task<IResult> UpdateStatus(
        Guid id,
        UpdateInvoiceStatusRequest request,
        AppDbContext db,
        CancellationToken ct)
    {
        var invoice = await db.Invoices.FindAsync([id], ct);
        if (invoice is null)
            return Results.NotFound();

        invoice.Status = request.Status;
        invoice.UpdatedAt = DateTime.UtcNow;

        if (request.Status == InvoiceStatus.Paid && invoice.PaidAt is null)
            invoice.PaidAt = DateOnly.FromDateTime(DateTime.UtcNow);
        else if (request.Status != InvoiceStatus.Paid)
            invoice.PaidAt = null;

        await db.SaveChangesAsync(ct);
        return Results.Ok(new { invoice.Id, invoice.Number, invoice.Status, invoice.PaidAt });
    }

    private static async Task<IResult> DeleteInvoice(Guid id, AppDbContext db, CancellationToken ct)
    {
        var invoice = await db.Invoices.FindAsync([id], ct);
        if (invoice is null)
            return Results.NotFound();

        if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Issued)
            return Results.Conflict(new { error = "Issued or paid invoices cannot be deleted. Cancel them instead." });

        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static InvoiceResponse ToResponse(Invoice i) => new(
        i.Id,
        i.Number,
        i.Status,
        i.CustomerId,
        i.Customer.Name,
        i.IssueDate,
        i.DueDate,
        i.DeliveryDate,
        i.PaidAt,
        i.Currency,
        i.VariableSymbol,
        i.ConstantSymbol,
        i.SpecificSymbol,
        i.PaymentMethod,
        i.BankIban,
        i.BankBic,
        i.Notes,
        i.Subtotal,
        i.VatTotal,
        i.Total,
        i.CreatedAt,
        i.UpdatedAt,
        i.Items
            .OrderBy(item => item.SortOrder)
            .Select(item => new InvoiceItemResponse(
                item.Id,
                item.Description,
                item.Unit,
                item.Quantity,
                item.UnitPrice,
                item.VatRate,
                item.Subtotal,
                item.VatAmount,
                item.Total,
                item.SortOrder
            ))
            .ToList()
    );
}
