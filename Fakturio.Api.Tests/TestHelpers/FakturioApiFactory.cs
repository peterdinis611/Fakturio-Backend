using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fakturio.Api.Tests.TestHelpers;

public sealed class FakturioApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"fakturio-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting("Cache:UseRedis", "false");
        builder.UseSetting("Cache:Enabled", "true");
        builder.UseSetting("OverdueSync:Enabled", "false");
        builder.UseSetting("ReminderJob:Enabled", "false");
        builder.UseSetting("RecurringInvoices:Enabled", "false");
        builder.UseSetting("DisableHttpsRedirection", "true");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
