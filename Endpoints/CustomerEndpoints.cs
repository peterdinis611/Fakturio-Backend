using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Endpoints;

public static class CustomerEndpoints
{
    public static RouteGroupBuilder MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/customers").WithTags("Customers");

        group.MapGet("/", ListCustomers);
        group.MapGet("/{id:guid}", GetCustomer);
        group.MapPost("/", CreateCustomer);
        group.MapPut("/{id:guid}", UpdateCustomer);
        group.MapDelete("/{id:guid}", DeleteCustomer);

        return group;
    }

    private static async Task<IResult> ListCustomers(AppDbContext db, CancellationToken ct)
    {
        var customers = await db.Customers
            .OrderBy(c => c.Name)
            .Select(c => ToResponse(c))
            .ToListAsync(ct);

        return Results.Ok(customers);
    }

    private static async Task<IResult> GetCustomer(Guid id, AppDbContext db, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([id], ct);
        return customer is null ? Results.NotFound() : Results.Ok(ToResponse(customer));
    }

    private static async Task<IResult> CreateCustomer(
        CreateCustomerRequest request,
        AppDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });

        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Ico = request.Ico,
            Dic = request.Dic,
            IcDph = request.IcDph,
            Email = request.Email,
            Phone = request.Phone,
            Street = request.Street,
            City = request.City,
            PostalCode = request.PostalCode,
            Country = string.IsNullOrWhiteSpace(request.Country) ? "SK" : request.Country.Trim().ToUpperInvariant()
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/customers/{customer.Id}", ToResponse(customer));
    }

    private static async Task<IResult> UpdateCustomer(
        Guid id,
        CreateCustomerRequest request,
        AppDbContext db,
        CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([id], ct);
        if (customer is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });

        customer.Name = request.Name.Trim();
        customer.Ico = request.Ico;
        customer.Dic = request.Dic;
        customer.IcDph = request.IcDph;
        customer.Email = request.Email;
        customer.Phone = request.Phone;
        customer.Street = request.Street;
        customer.City = request.City;
        customer.PostalCode = request.PostalCode;
        customer.Country = string.IsNullOrWhiteSpace(request.Country)
            ? customer.Country
            : request.Country.Trim().ToUpperInvariant();

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToResponse(customer));
    }

    private static async Task<IResult> DeleteCustomer(Guid id, AppDbContext db, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([id], ct);
        if (customer is null)
            return Results.NotFound();

        var hasInvoices = await db.Invoices.AnyAsync(i => i.CustomerId == id, ct);
        if (hasInvoices)
            return Results.Conflict(new { error = "Customer has invoices and cannot be deleted." });

        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static CustomerResponse ToResponse(Customer c) => new(
        c.Id,
        c.Name,
        c.Ico,
        c.Dic,
        c.IcDph,
        c.Email,
        c.Phone,
        c.Street,
        c.City,
        c.PostalCode,
        c.Country,
        c.CreatedAt
    );
}
