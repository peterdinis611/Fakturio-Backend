using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fakturio.Api.Tests;

public sealed class FakturioApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"fakturio-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
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

public class LoginCreateInvoiceSmokeTests : IClassFixture<FakturioApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client;

    public LoginCreateInvoiceSmokeTests(FakturioApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_then_create_draft_invoice()
    {
        var health = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        var healthBody = await health.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", healthBody.GetProperty("status").GetString());
        Assert.Equal("up", healthBody.GetProperty("database").GetString());

        var loginRes = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            DemoDataSeeder.Email,
            DemoDataSeeder.Password));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var create = new CreateInvoiceRequest(
            CustomerId: Guid.Parse("a3333333-3333-3333-3333-333333333301"),
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
            Notes: "smoke test",
            Status: InvoiceStatus.Draft,
            DocumentType: InvoiceDocumentType.Invoice,
            Flow: DocumentFlow.Issued,
            Items:
            [
                new CreateInvoiceItemRequest(
                    Description: "Smoke line",
                    Unit: "ks",
                    Quantity: 2,
                    UnitPrice: 50m,
                    VatRate: 23m),
            ]);

        var invoiceRes = await _client.PostAsJsonAsync("/api/invoices", create);
        Assert.True(
            invoiceRes.IsSuccessStatusCode,
            await invoiceRes.Content.ReadAsStringAsync());

        var invoice = await invoiceRes.Content.ReadFromJsonAsync<InvoiceResponse>(JsonOptions);
        Assert.NotNull(invoice);
        Assert.Equal(100.00m, invoice.Subtotal);
        Assert.Equal(23.00m, invoice.VatTotal);
        Assert.Equal(123.00m, invoice.Total);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
    }
}
