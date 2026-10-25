using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Customers;

public class CustomerService(AppDbContext db) : ICustomerService
{
    public async Task<PagedResponse<CustomerResponse>> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default)
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

    public async Task<CustomerResponse?> GetAsync(Guid id, CancellationToken ct = default)
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

    public async Task<ServiceResult<CustomerResponse>> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Ico))
        {
            var exists = await db.Customers.AnyAsync(c => c.Ico == request.Ico.Trim(), ct);
            if (exists)
                return ServiceResult<CustomerResponse>.Conflict(
                    "A customer with this IČO already exists.",
                    ErrorCodes.DuplicateIco);
        }

        var customer = new Customer();
        CustomerMapper.Apply(customer, request);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return ServiceResult<CustomerResponse>.Created(CustomerMapper.ToResponse(customer));
    }

    public async Task<ServiceResult<CustomerResponse>> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken ct = default)
    {
        var customer = await db.Customers.FindAsync([id], ct);
        if (customer is null)
            return ServiceResult<CustomerResponse>.NotFound("Customer not found.", ErrorCodes.NotFound);

        if (!string.IsNullOrWhiteSpace(request.Ico))
        {
            var ico = request.Ico.Trim();
            var exists = await db.Customers.AnyAsync(c => c.Id != id && c.Ico == ico, ct);
            if (exists)
                return ServiceResult<CustomerResponse>.Conflict(
                    "A customer with this IČO already exists.",
                    ErrorCodes.DuplicateIco);
        }

        CustomerMapper.Apply(customer, request);
        await db.SaveChangesAsync(ct);

        var invoiceCount = await db.Invoices.CountAsync(i => i.CustomerId == id, ct);
        return ServiceResult<CustomerResponse>.Ok(CustomerMapper.ToResponse(customer, invoiceCount));
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await db.Customers.FindAsync([id], ct);
        if (customer is null)
            return ServiceResult.NotFound("Customer not found.", ErrorCodes.NotFound);

        var hasInvoices = await db.Invoices.AnyAsync(i => i.CustomerId == id, ct);
        if (hasInvoices)
            return ServiceResult.Conflict(
                "Customer has invoices and cannot be deleted.",
                ErrorCodes.HasInvoices);

        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }
}
