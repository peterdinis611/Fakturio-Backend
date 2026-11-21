using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fakturio.Api.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fakturio.Api.Tests;

/// <summary>
/// Own host with rate limiting on — must not run in parallel with ApiFactory
/// (Serilog bootstrap logger freezes after the first host build).
/// </summary>
[Collection(RateLimitingCollection.Name)]
public class RateLimitingTests
{
    [Fact]
    public async Task Auth_login_returns_rate_limited_when_exceeded()
    {
        await using var factory = new RateLimitedApiFactory();
        var client = factory.CreateClient();

        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
        {
            last = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest("nobody@example.com", "WrongPass1!"));
        }

        Assert.NotNull(last);
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);

        await using var stream = await last.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        Assert.Equal("rate_limited", doc.RootElement.GetProperty("code").GetString());
        Assert.True(last.Headers.Contains("Retry-After") || last.Headers.Contains("X-RateLimit-Remaining"));
    }

    private sealed class RateLimitedApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath =
            Path.Combine(Path.GetTempPath(), $"fakturio-rl-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
            builder.UseSetting("Cache:UseRedis", "false");
            builder.UseSetting("Cache:Enabled", "false");
            builder.UseSetting("BullMQ:Enabled", "false");
            builder.UseSetting("OverdueSync:Enabled", "false");
            builder.UseSetting("ReminderJob:Enabled", "false");
            builder.UseSetting("RecurringInvoices:Enabled", "false");
            builder.UseSetting("DisableHttpsRedirection", "true");
            builder.UseSetting("Email:Provider", "Virtual");
            builder.UseSetting("RateLimiting:Enabled", "true");
            builder.UseSetting("RateLimiting:AuthPermitLimit", "2");
            builder.UseSetting("RateLimiting:AuthWindowSeconds", "60");
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
                // best-effort
            }
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RateLimitingCollection
{
    public const string Name = "RateLimiting";
}
