using System.Net;
using System.Net.Http.Json;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Tests.TestHelpers;

namespace Fakturio.Api.Tests;

[Collection(ApiFactoryCollection.Name)]
public class GdprExportTests
{
    private readonly FakturioApiFactory _factory;

    public GdprExportTests(FakturioApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Export_returns_zip_and_writes_audit()
    {
        var client = _factory.CreateClient();
        await DemoAuth.LoginAsDemoAsync(client);

        var res = await client.GetAsync("/api/gdpr/export.zip");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        Assert.Equal("application/zip", res.Content.Headers.ContentType?.MediaType);

        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 4);
        // ZIP local file header magic
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);

        var logs = await client.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            "/api/logs?action=GdprExport&pageSize=5",
            TestJson.Options);
        Assert.NotNull(logs);
        Assert.Contains(logs.Items, l => l.Action == AuditAction.GdprExport);
    }

    [Fact]
    public async Task Delete_account_rejects_bad_password_and_erases_valid_user()
    {
        var client = _factory.CreateClient();
        var email = $"gdpr-{Guid.NewGuid():N}@example.com";
        const string password = "Test1234!";
        await DemoAuth.RegisterAndLoginAsync(client, email, password);

        // Ensure company exists before delete.
        (await client.GetAsync("/api/customers")).EnsureSuccessStatusCode();

        var bad = await client.PostAsJsonAsync(
            "/api/gdpr/delete-account",
            new DeleteAccountRequest("WrongPass!", "DELETE"));
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);

        var ok = await client.PostAsJsonAsync(
            "/api/gdpr/delete-account",
            new DeleteAccountRequest(password, "ZMAZAT"));
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        var loginAfter = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.Unauthorized, loginAfter.StatusCode);
    }
}
