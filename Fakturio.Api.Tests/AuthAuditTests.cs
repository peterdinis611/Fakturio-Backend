using System.Net;
using System.Net.Http.Json;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Tests.TestHelpers;

namespace Fakturio.Api.Tests;

public class AuthAuditTests : IClassFixture<FakturioApiFactory>
{
    private readonly FakturioApiFactory _factory;

    public AuthAuditTests(FakturioApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_with_bad_password_returns_unauthorized()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            DemoDataSeeder.Email,
            "WrongPass!"));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_writes_LoginSucceeded_audit_and_me_works()
    {
        var client = _factory.CreateClient();
        var auth = await DemoAuth.LoginAsDemoAsync(client);

        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.Equal(DemoDataSeeder.Email, auth.User.Email);

        var me = await client.GetFromJsonAsync<UserResponse>("/api/auth/me", TestJson.Options);
        Assert.NotNull(me);
        Assert.Equal(auth.User.Id, me.Id);

        var logs = await client.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            "/api/logs?action=LoginSucceeded&pageSize=10",
            TestJson.Options);

        Assert.NotNull(logs);
        Assert.Contains(logs.Items, l =>
            l.Action == AuditAction.LoginSucceeded &&
            l.EntityType == "Auth");
    }

    [Fact]
    public async Task Logs_are_tenant_isolated()
    {
        var demoClient = _factory.CreateClient();
        await DemoAuth.LoginAsDemoAsync(demoClient);
        var demoLogs = await demoClient.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            "/api/logs?pageSize=100",
            TestJson.Options);
        Assert.NotNull(demoLogs);
        Assert.Contains(demoLogs.Items, l =>
            l.EntityNumber is not null && l.EntityNumber.StartsWith("FA2026", StringComparison.Ordinal));

        var other = _factory.CreateClient();
        var email = $"tenant-{Guid.NewGuid():N}@example.com";
        await DemoAuth.RegisterAndLoginAsync(other, email);

        // Touch a tenant-scoped endpoint so CompanySettings is created.
        var customers = await other.GetAsync("/api/customers");
        customers.EnsureSuccessStatusCode();

        var otherLogs = await other.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            "/api/logs?pageSize=100",
            TestJson.Options);
        Assert.NotNull(otherLogs);
        Assert.DoesNotContain(otherLogs.Items, l =>
            l.EntityNumber is not null && l.EntityNumber.StartsWith("FA2026", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_unauthorized()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
