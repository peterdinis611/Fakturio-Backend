using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Company;
using Fakturio.Api.Services.Customers;
using Fakturio.Api.Services.Invoices;

namespace Fakturio.Api.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IInvoiceNumberService, InvoiceNumberService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IInvoicePdfService, InvoicePdfService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ICompanyService, CompanyService>();
        return services;
    }
}
