using System.Net;
using System.Net.Http.Json;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Tests.TestHelpers;

namespace Fakturio.Api.Tests;

[Collection(ApiFactoryCollection.Name)]
public class RecurringInvoiceTests
{
    private readonly HttpClient _client;

    public RecurringInvoiceTests(FakturioApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_generate_and_delete_recurring_invoice()
    {
        await DemoAuth.LoginAsDemoAsync(_client);

        var create = new CreateRecurringInvoiceRequest(
            CustomerId: DemoAuth.DemoCustomerStudioId,
            Name: $"Recurring {Guid.NewGuid():N}"[..20],
            Interval: RecurrenceInterval.Monthly,
            DayOfMonth: 1,
            DueDays: 14,
            NextRunOn: DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive: true,
            Notes: "test recurring",
            Items:
            [
                new RecurringInvoiceItemRequest(null, "Mesačný retainer", "mes", 1, 500m, 23m),
            ]);

        var createRes = await _client.PostAsJsonAsync("/api/recurring-invoices", create);
        Assert.True(createRes.IsSuccessStatusCode, await createRes.Content.ReadAsStringAsync());
        var recurring = await createRes.Content.ReadFromJsonAsync<RecurringInvoiceResponse>(TestJson.Options);
        Assert.NotNull(recurring);
        Assert.True(recurring.IsActive);

        var generateRes = await _client.PostAsync($"/api/recurring-invoices/{recurring.Id}/generate", null);
        Assert.True(generateRes.IsSuccessStatusCode, await generateRes.Content.ReadAsStringAsync());
        var invoice = await generateRes.Content.ReadFromJsonAsync<InvoiceResponse>(TestJson.Options);
        Assert.NotNull(invoice);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(500m, invoice.Subtotal);

        var createdLogs = await _client.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            $"/api/logs?entityId={recurring.Id}&pageSize=20",
            TestJson.Options);
        Assert.NotNull(createdLogs);
        Assert.Contains(createdLogs.Items, l => l.Action == AuditAction.RecurringCreated);

        // RecurringGenerated is recorded against the generated invoice, not the template.
        var generatedLogs = await _client.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            $"/api/logs?entityId={invoice.Id}&action=RecurringGenerated&pageSize=5",
            TestJson.Options);
        Assert.NotNull(generatedLogs);
        Assert.Contains(generatedLogs.Items, l => l.Action == AuditAction.RecurringGenerated);

        var deleteRes = await _client.DeleteAsync($"/api/recurring-invoices/{recurring.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var afterDelete = await _client.GetAsync($"/api/recurring-invoices/{recurring.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }
}
