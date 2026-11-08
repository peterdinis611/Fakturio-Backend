using System.Net;
using System.Net.Http.Json;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Tests.TestHelpers;

namespace Fakturio.Api.Tests;

[Collection(ApiFactoryCollection.Name)]
public class CustomerAuditTests
{
    private readonly HttpClient _client;

    public CustomerAuditTests(FakturioApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_update_delete_customer_writes_audit_actions()
    {
        await DemoAuth.LoginAsDemoAsync(_client);

        var createReq = new CreateCustomerRequest(
            Name: $"Test Customer {Guid.NewGuid():N}"[..24],
            Ico: "12345679",
            Dic: "1234567890",
            IcDph: "SK1234567890",
            Email: "customer@example.com",
            Phone: null,
            Street: "Hlavná 1",
            City: "Bratislava",
            PostalCode: "811 01",
            Country: "SK");

        var createRes = await _client.PostAsJsonAsync("/api/customers", createReq);
        Assert.True(createRes.IsSuccessStatusCode, await createRes.Content.ReadAsStringAsync());
        var created = await createRes.Content.ReadFromJsonAsync<CustomerResponse>(TestJson.Options);
        Assert.NotNull(created);
        Assert.Equal("12345679", created.Ico);

        var updateReq = createReq with { Name = created.Name + " Updated", City = "Košice" };
        var updateRes = await _client.PutAsJsonAsync($"/api/customers/{created.Id}", updateReq);
        Assert.True(updateRes.IsSuccessStatusCode, await updateRes.Content.ReadAsStringAsync());

        var deleteRes = await _client.DeleteAsync($"/api/customers/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var logs = await _client.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            $"/api/logs?entityId={created.Id}&pageSize=20",
            TestJson.Options);
        Assert.NotNull(logs);

        Assert.Contains(logs.Items, l => l.Action == AuditAction.CustomerCreated);
        Assert.Contains(logs.Items, l => l.Action == AuditAction.CustomerUpdated);
        Assert.Contains(logs.Items, l => l.Action == AuditAction.CustomerDeleted);
    }

    [Fact]
    public async Task Create_customer_with_invalid_ico_returns_bad_request()
    {
        await DemoAuth.LoginAsDemoAsync(_client);

        var createReq = new CreateCustomerRequest(
            Name: "Bad ICO Co",
            Ico: "12345678",
            Dic: null,
            IcDph: null,
            Email: null,
            Phone: null,
            Street: null,
            City: null,
            PostalCode: null,
            Country: "SK");

        var res = await _client.PostAsJsonAsync("/api/customers", createReq);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
