using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services;

public class CustomerService(AppDbContext db)
{
    public async Task<PagedResponse<CustomerResponse>> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c =>
                c.Name.Contains(term) ||
                (c.Ico != null && c.Ico.Contains(term)) ||
                (c.Email != null && c.Email.Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);
        var customers = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                Customer = c,
                InvoiceCount = c.Invoices.Count
            })
            .ToListAsync(ct);

        return new PagedResponse<CustomerResponse>(
            customers.Select(x => CustomerMapper.ToResponse(x.Customer, x.InvoiceCount)).ToList(),
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<CustomerResponse?> GetAsync(Guid id, CancellationToken ct)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new { Customer = c, InvoiceCount = c.Invoices.Count })
            .FirstOrDefaultAsync(ct);

        return customer is null
            ? null
            : CustomerMapper.ToResponse(customer.Customer, customer.InvoiceCount);
    }

    public async Task<(CustomerResponse? Customer, string? Error)> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.Ico))
        {
            var exists = await db.Customers.AnyAsync(c => c.Ico == request.Ico.Trim(), ct);
            if (exists)
                return (null, "A customer with this IČO already exists.");
        }

        var customer = new Customer();
        CustomerMapper.Apply(customer, request);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return (CustomerMapper.ToResponse(customer), null);
    }

    public async Task<(CustomerResponse? Customer, string? Error, int StatusCode)> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([id], ct);
        if (customer is null)
            return (null, "Customer not found.", StatusCodes.Status404NotFound);

        if (!string.IsNullOrWhiteSpace(request.Ico))
        {
            var ico = request.Ico.Trim();
            var exists = await db.Customers.AnyAsync(c => c.Id != id && c.Ico == ico, ct);
            if (exists)
                return (null, "A customer with this IČO already exists.", StatusCodes.Status409Conflict);
        }

        CustomerMapper.Apply(customer, request);
        await db.SaveChangesAsync(ct);

        var invoiceCount = await db.Invoices.CountAsync(i => i.CustomerId == id, ct);
        return (CustomerMapper.ToResponse(customer, invoiceCount), null, StatusCodes.Status200OK);
    }

    public async Task<(bool Ok, string? Error, int StatusCode)> DeleteAsync(Guid id, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([id], ct);
        if (customer is null)
            return (false, "Customer not found.", StatusCodes.Status404NotFound);

        var hasInvoices = await db.Invoices.AnyAsync(i => i.CustomerId == id, ct);
        if (hasInvoices)
            return (false, "Customer has invoices and cannot be deleted.", StatusCodes.Status409Conflict);

        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);
        return (true, null, StatusCodes.Status204NoContent);
    }
}
