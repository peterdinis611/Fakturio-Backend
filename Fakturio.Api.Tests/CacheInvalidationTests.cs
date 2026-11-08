using System.Net;
using Fakturio.Api.Tests.TestHelpers;

namespace Fakturio.Api.Tests;

[Collection(ApiFactoryCollection.Name)]
public class CacheInvalidationTests
{
    private readonly FakturioApiFactory _factory;

    public CacheInvalidationTests(FakturioApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Invalidate_returns_no_content()
    {
        var client = _factory.CreateClient();
        await DemoAuth.LoginAsDemoAsync(client);

        var res = await client.PostAsync("/api/cache/invalidate", null);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Invalidate_without_auth_returns_unauthorized()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/cache/invalidate", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
