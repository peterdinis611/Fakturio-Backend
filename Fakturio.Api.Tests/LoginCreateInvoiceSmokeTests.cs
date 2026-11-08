using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Tests.TestHelpers;

namespace Fakturio.Api.Tests;

[Collection(ApiFactoryCollection.Name)]
public class LoginCreateInvoiceSmokeTests
{
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
        var healthBody = await health.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        Assert.Equal("ok", healthBody.GetProperty("status").GetString());
        Assert.Equal("up", healthBody.GetProperty("database").GetString());

        await DemoAuth.LoginAsDemoAsync(_client);

        var create = new CreateInvoiceRequest(
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

        var invoice = await invoiceRes.Content.ReadFromJsonAsync<InvoiceResponse>(TestJson.Options);
        Assert.NotNull(invoice);
        Assert.Equal(100.00m, invoice.Subtotal);
        Assert.Equal(23.00m, invoice.VatTotal);
        Assert.Equal(123.00m, invoice.Total);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
    }
}
