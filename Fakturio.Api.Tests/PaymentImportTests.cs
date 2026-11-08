using System.Net.Http.Json;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Tests.TestHelpers;

namespace Fakturio.Api.Tests;

[Collection(ApiFactoryCollection.Name)]
public class PaymentImportTests
{
    private readonly HttpClient _client;

    public PaymentImportTests(FakturioApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Preview_and_confirm_marks_matched_invoice_paid()
    {
        await DemoAuth.LoginAsDemoAsync(_client);

        // Seed FA2026002 is Issued with VS 2026002 — use it for matching.
        var invoice = await _client.GetFromJsonAsync<InvoiceResponse>(
            "/api/invoices/by-number/FA2026002",
            TestJson.Options);
        Assert.NotNull(invoice);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Equal("2026002", invoice.VariableSymbol);

        var csv = $"""
            VS;Suma;Datum;Protistrana
            {invoice.VariableSymbol};{invoice.Total.ToString(System.Globalization.CultureInfo.InvariantCulture)};{DateTime.UtcNow:dd.MM.yyyy};Nordic Apps
            """;

        var previewRes = await _client.PostAsJsonAsync(
            "/api/payments/import/preview",
            new PaymentImportPreviewRequest(csv));
        Assert.True(previewRes.IsSuccessStatusCode, await previewRes.Content.ReadAsStringAsync());
        var preview = await previewRes.Content.ReadFromJsonAsync<PaymentImportPreviewResponse>(TestJson.Options);
        Assert.NotNull(preview);
        Assert.Equal(1, preview.Matched);
        Assert.Equal("matched", preview.Rows[0].MatchStatus);
        Assert.Equal(invoice.Id, preview.Rows[0].InvoiceId);

        var confirmRes = await _client.PostAsJsonAsync(
            "/api/payments/import/confirm",
            new PaymentImportConfirmRequest(
            [
                new PaymentImportConfirmRow(invoice.Id, DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")),
            ]));
        Assert.True(confirmRes.IsSuccessStatusCode, await confirmRes.Content.ReadAsStringAsync());
        var confirm = await confirmRes.Content.ReadFromJsonAsync<PaymentImportConfirmResponse>(TestJson.Options);
        Assert.NotNull(confirm);
        Assert.Equal(1, confirm.MarkedPaid);
        Assert.Equal(0, confirm.Failed);

        var paid = await _client.GetFromJsonAsync<InvoiceResponse>(
            $"/api/invoices/{invoice.Id}",
            TestJson.Options);
        Assert.NotNull(paid);
        Assert.Equal(InvoiceStatus.Paid, paid.Status);

        var logs = await _client.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            "/api/logs?action=PaymentImportApplied&pageSize=5",
            TestJson.Options);
        Assert.NotNull(logs);
        Assert.Contains(logs.Items, l => l.Action == AuditAction.PaymentImportApplied);
    }

    [Fact]
    public async Task Preview_without_vs_column_returns_bad_request()
    {
        await DemoAuth.LoginAsDemoAsync(_client);

        var res = await _client.PostAsJsonAsync(
            "/api/payments/import/preview",
            new PaymentImportPreviewRequest("Suma;Datum\n100;01.01.2026"));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
    }
}
