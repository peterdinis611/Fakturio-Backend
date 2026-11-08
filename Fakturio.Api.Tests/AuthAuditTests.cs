using System.Net;
using System.Net.Http.Json;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Tests.TestHelpers;

namespace Fakturio.Api.Tests;

[Collection(ApiFactoryCollection.Name)]
public class AuthAuditTests
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

        var createRes = await demoClient.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(
            CustomerId: DemoAuth.DemoCustomerStudioId,
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            DeliveryDate: null,
            Currency: "EUR",
            VariableSymbol: null,
            ConstantSymbol: null,
            SpecificSymbol: null,
            PaymentMethod: "bank_transfer",
            BankIban: null,
            BankBic: null,
            Notes: "tenant isolation",
            Status: InvoiceStatus.Draft,
            DocumentType: InvoiceDocumentType.Invoice,
            Flow: DocumentFlow.Issued,
            Items: [new CreateInvoiceItemRequest("Isolation", "ks", 1, 10m, 23m)]));
        Assert.True(createRes.IsSuccessStatusCode, await createRes.Content.ReadAsStringAsync());
        var invoice = await createRes.Content.ReadFromJsonAsync<InvoiceResponse>(TestJson.Options);
        Assert.NotNull(invoice);

        var demoLogs = await demoClient.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            $"/api/logs?entityId={invoice.Id}&pageSize=20",
            TestJson.Options);
        Assert.NotNull(demoLogs);
        Assert.NotEmpty(demoLogs.Items);

        var other = _factory.CreateClient();
        var email = $"tenant-{Guid.NewGuid():N}@example.com";
        await DemoAuth.RegisterAndLoginAsync(other, email);
        (await other.GetAsync("/api/customers")).EnsureSuccessStatusCode();

        var otherLogs = await other.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            $"/api/logs?entityId={invoice.Id}&pageSize=20",
            TestJson.Options);
        Assert.NotNull(otherLogs);
        Assert.Empty(otherLogs.Items);
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_unauthorized()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
