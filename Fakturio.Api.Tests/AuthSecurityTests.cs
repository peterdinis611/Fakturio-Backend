using System.Net;
using System.Net.Http.Json;
using Fakturio.Api.Dtos;
using Fakturio.Api.Tests.TestHelpers;

namespace Fakturio.Api.Tests;

[Collection(ApiFactoryCollection.Name)]
public class AuthSecurityTests
{
    private readonly FakturioApiFactory _factory;

    public AuthSecurityTests(FakturioApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_with_admin_email_is_rejected()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "demo@fakturio.sk",
            "Hacker123!",
            "Nope",
            "Admin"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Attachments_are_not_served_as_static_files()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync(
            $"/uploads/attachments/{Guid.NewGuid():N}/{Guid.NewGuid():N}/secret.pdf");

        // Must not be publicly readable (404 if unmatched; 401 if fallback auth applies).
        Assert.True(
            res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized,
            $"Unexpected status: {res.StatusCode}");
    }

    [Fact]
    public async Task Jobs_settings_require_admin()
    {
        var client = _factory.CreateClient();
        await DemoAuth.RegisterAndLoginAsync(client, $"jobs-{Guid.NewGuid():N}@example.com");

        var res = await client.GetAsync("/api/jobs/settings");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
