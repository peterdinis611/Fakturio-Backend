using System.Net.Http.Headers;
using Fakturio.Api.Caching;
using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Audit;
using Fakturio.Api.Services.Auth;
using Fakturio.Api.Services.Company;
using Microsoft.AspNetCore.Identity;
using Fakturio.Api.Services.Customers;
using Fakturio.Api.Services.Exports;
using Fakturio.Api.Services.Invoices;
using Fakturio.Api.Services.Jobs;
using Fakturio.Api.Services.Notes;
using Fakturio.Api.Services.Payments;
using Fakturio.Api.Services.Registry;
using Fakturio.Api.Services.Reminders;
using Fakturio.Api.Services.Reports;
using Fakturio.Api.Services.Resume;
using Fakturio.Api.Services.Search;
using Fakturio.Api.Services.ServiceCatalog;
using Fakturio.Api.Services.Users;

namespace Fakturio.Api.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OverdueSyncOptions>(configuration.GetSection(OverdueSyncOptions.SectionName));
        services.Configure<ReminderJobOptions>(configuration.GetSection(ReminderJobOptions.SectionName));
        services.Configure<RegistryOptions>(configuration.GetSection(RegistryOptions.SectionName));
        services.Configure<LogoStorageOptions>(configuration.GetSection(LogoStorageOptions.SectionName));
        services.AddSingleton<IJobSettingsStore, JobSettingsStore>();
        services.AddSingleton<ILogoStorageService, LogoStorageService>();

        var registry = configuration.GetSection(RegistryOptions.SectionName).Get<RegistryOptions>()
            ?? new RegistryOptions();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(registry.TimeoutSeconds, 5, 60));

        services.AddHttpClient("ares", client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(registry.AresBaseUrl));
            client.Timeout = timeout;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddHttpClient("rpo", client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(registry.RpoBaseUrl));
            client.Timeout = timeout;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddHttpClient("subjekt", client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(registry.SubjektBaseUrl));
            client.Timeout = timeout;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddScoped<ICompanyRegistryService, CompanyRegistryService>();

        services.AddHostedService<OverdueSyncBackgroundService>();
        services.AddScoped<IReminderNotifier, LoggingReminderNotifier>();
        services.AddHostedService<ReminderBackgroundService>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActorAccessor, CurrentActorAccessor>();
        services.AddScoped<IAuditLogService, AuditLogService>();

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
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IPaymentImportService, PaymentImportService>();
        services.AddScoped<IResumeService, ResumeService>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

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

    private static string EnsureTrailingSlash(string url) =>
        url.EndsWith('/') ? url : url + "/";
}
