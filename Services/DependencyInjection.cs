using System.Net.Http.Headers;
using Fakturio.Api.Caching;
using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Admin;
using Fakturio.Api.Services.Audit;
using Fakturio.Api.Services.Auth;
using Fakturio.Api.Services.Company;
using Microsoft.AspNetCore.Identity;
using Fakturio.Api.Services.Customers;
using Fakturio.Api.Services.Exports;
using Fakturio.Api.Services.Invoices;
using Fakturio.Api.Services.Jobs;
using Fakturio.Api.Services.Notes;
using Fakturio.Api.Services.Address;
using Fakturio.Api.Services.Calendar;
using Fakturio.Api.Services.Payments;
using Fakturio.Api.Services.Fx;
using Fakturio.Api.Services.Iban;
using Fakturio.Api.Services.Registry;
using Fakturio.Api.Services.Reminders;
using Fakturio.Api.Services.Notifications;
using Fakturio.Api.Services.Reports;
using Fakturio.Api.Services.Resume;
using Fakturio.Api.Services.Search;
using Fakturio.Api.Services.ServiceCatalog;
using Fakturio.Api.Services.Tenancy;
using Fakturio.Api.Services.Users;
using Fakturio.Api.Services.Gdpr;
using Fakturio.Api.Services.Recurring;

namespace Fakturio.Api.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OverdueSyncOptions>(configuration.GetSection(OverdueSyncOptions.SectionName));
        services.Configure<ReminderJobOptions>(configuration.GetSection(ReminderJobOptions.SectionName));
        services.Configure<RecurringJobOptions>(configuration.GetSection(RecurringJobOptions.SectionName));
        services.Configure<RegistryOptions>(configuration.GetSection(RegistryOptions.SectionName));
        services.Configure<FxOptions>(configuration.GetSection(FxOptions.SectionName));
        services.Configure<LogoStorageOptions>(configuration.GetSection(LogoStorageOptions.SectionName));
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
        services.AddSingleton<IAdminAccess, AdminAccess>();
        services.AddSingleton<IJobSettingsStore, JobSettingsStore>();
        services.AddSingleton<ILogoStorageService, LogoStorageService>();
        services.AddScoped<IAdminService, AdminService>();

        var registry = configuration.GetSection(RegistryOptions.SectionName).Get<RegistryOptions>()
            ?? new RegistryOptions();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(registry.TimeoutSeconds, 5, 60));
        var fx = configuration.GetSection(FxOptions.SectionName).Get<FxOptions>() ?? new FxOptions();

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
        services.AddHttpClient("orsf", client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(registry.OrsfBaseUrl));
            client.Timeout = timeout;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Fakturio/1.0");
        });
        services.AddHttpClient("vies", client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(registry.ViesBaseUrl));
            client.Timeout = timeout;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddHttpClient("frankfurter", client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(fx.FrankfurterBaseUrl));
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(fx.TimeoutSeconds, 5, 30));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddHttpClient("cnb", client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(fx.CnbBaseUrl));
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(fx.TimeoutSeconds, 5, 30));
        });
        services.AddHttpClient("nbs", client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(fx.NbsBaseUrl));
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(fx.TimeoutSeconds, 5, 30));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Fakturio/1.0");
        });
        services.AddHttpClient("openiban", client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(registry.OpenIbanBaseUrl));
            client.Timeout = timeout;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddHttpClient("nominatim", client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(registry.NominatimBaseUrl));
            client.Timeout = timeout;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Fakturio/1.0 (invoice-app; contact@fakturio.local)");
        });
        services.AddHttpClient("orsr", client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(registry.OrsrBaseUrl));
            client.Timeout = timeout;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Fakturio/1.0");
        });
        services.AddHttpClient("adisreg", client =>
        {
            // Full SOAP endpoint — keep exact path (no trailing slash).
            client.BaseAddress = new Uri(registry.AdisregBaseUrl.TrimEnd('/'));
            client.Timeout = timeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Fakturio/1.0");
        });
        services.AddHttpClient("isir", client =>
        {
            client.BaseAddress = new Uri(registry.IsirBaseUrl.TrimEnd('/'));
            client.Timeout = timeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Fakturio/1.0");
        });
        services.AddHttpClient("registeruz", client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(registry.RegisterUzBaseUrl));
            client.Timeout = timeout;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Fakturio/1.0");
        });

        services.AddScoped<ICompanyRegistryService, CompanyRegistryService>();
        services.AddScoped<IVatVerificationService, VatVerificationService>();
        services.AddScoped<IFsVatPayerService, FsVatPayerService>();
        services.AddScoped<IOrsrService, OrsrService>();
        services.AddScoped<IAdisregService, AdisregService>();
        services.AddScoped<IIsirService, IsirService>();
        services.AddScoped<IRegisterUzService, RegisterUzService>();
        services.AddScoped<IAresVrService, AresVrService>();
        services.AddScoped<IIbanLookupService, IbanLookupService>();
        services.AddSingleton<IAddressNormalizeService, NominatimAddressService>();
        services.AddSingleton<IPostalValidateService, PostalValidateService>();
        services.AddSingleton<IBusinessDayService, BusinessDayService>();
        services.AddSingleton<FrankfurterFxRateService>();
        services.AddSingleton<CnbFxRateService>();
        services.AddSingleton<NbsFxRateService>();
        services.AddSingleton<IFxRateService, CompositeFxRateService>();

        services.AddHostedService<OverdueSyncBackgroundService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReminderNotifier, SignalHubReminderNotifier>();
        services.AddHostedService<ReminderBackgroundService>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActorAccessor, CurrentActorAccessor>();
        services.AddScoped<ITenantContext, TenantContext>();
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
        services.AddScoped<IGdprService, GdprService>();
        services.AddScoped<IRecurringInvoiceService, RecurringInvoiceService>();
        services.AddHostedService<RecurringInvoiceBackgroundService>();
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
