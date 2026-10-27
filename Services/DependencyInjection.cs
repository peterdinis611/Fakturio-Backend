using Fakturio.Api.Caching;
using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Company;
using Fakturio.Api.Services.Customers;
using Fakturio.Api.Services.Exports;
using Fakturio.Api.Services.Invoices;
using Fakturio.Api.Services.Jobs;
using Fakturio.Api.Services.Reminders;
using Fakturio.Api.Services.Reports;
using Fakturio.Api.Services.Search;

namespace Fakturio.Api.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OverdueSyncOptions>(configuration.GetSection(OverdueSyncOptions.SectionName));
        services.Configure<ReminderJobOptions>(configuration.GetSection(ReminderJobOptions.SectionName));
        services.AddSingleton<IJobSettingsStore, JobSettingsStore>();

        services.AddHostedService<OverdueSyncBackgroundService>();
        services.AddScoped<IReminderNotifier, LoggingReminderNotifier>();
        services.AddHostedService<ReminderBackgroundService>();

        services.AddScoped<IInvoiceNumberService, InvoiceNumberService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IInvoicePdfService, InvoicePdfService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISearchService, SearchService>();

        services.AddScoped<CompanyService>();
        services.AddScoped<ICompanyService, CachingCompanyService>();

        services.AddScoped<ReportService>();
        services.AddScoped<IReportService, CachingReportService>();

        services.AddScoped<ICacheInvalidator, CacheInvalidator>();
        services.AddScoped<IExportService, CsvExportService>();

        return services;
    }

    public static IServiceCollection AddApplicationCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.AddHybridCache();
        return services;
    }
}
