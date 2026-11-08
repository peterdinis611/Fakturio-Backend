using System.Net;
using System.Net.Http.Json;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Tests.TestHelpers;

namespace Fakturio.Api.Tests;

[Collection(ApiFactoryCollection.Name)]
public class InvoiceLifecycleTests
{
    private readonly HttpClient _client;

    public InvoiceLifecycleTests(FakturioApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Draft_issue_pay_cancel_and_duplicate_flow()
    {
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
            Notes: "lifecycle",
            Status: InvoiceStatus.Draft,
            DocumentType: InvoiceDocumentType.Invoice,
            Flow: DocumentFlow.Issued,
            Items:
            [
                new CreateInvoiceItemRequest("Lifecycle line", "ks", 1, 100m, 23m),
            ]);

        var createRes = await _client.PostAsJsonAsync("/api/invoices", create);
        Assert.True(createRes.IsSuccessStatusCode, await createRes.Content.ReadAsStringAsync());
        var draft = await createRes.Content.ReadFromJsonAsync<InvoiceResponse>(TestJson.Options);
        Assert.NotNull(draft);
        Assert.Equal(InvoiceStatus.Draft, draft.Status);

        var issueRes = await _client.PatchAsJsonAsync(
            $"/api/invoices/{draft.Id}/status",
            new UpdateInvoiceStatusRequest(InvoiceStatus.Issued));
        Assert.True(issueRes.IsSuccessStatusCode, await issueRes.Content.ReadAsStringAsync());
        var issued = await issueRes.Content.ReadFromJsonAsync<InvoiceStatusResponse>(TestJson.Options);
        Assert.NotNull(issued);
        Assert.Equal(InvoiceStatus.Issued, issued.Status);

        var byNumber = await _client.GetFromJsonAsync<InvoiceResponse>(
            $"/api/invoices/by-number/{Uri.EscapeDataString(draft.Number)}",
            TestJson.Options);
        Assert.NotNull(byNumber);
        Assert.Equal(draft.Id, byNumber.Id);

        var payRes = await _client.PostAsJsonAsync(
            $"/api/invoices/{draft.Id}/pay",
            new MarkInvoicePaidRequest(DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.True(payRes.IsSuccessStatusCode, await payRes.Content.ReadAsStringAsync());
        var paid = await payRes.Content.ReadFromJsonAsync<InvoiceStatusResponse>(TestJson.Options);
        Assert.NotNull(paid);
        Assert.Equal(InvoiceStatus.Paid, paid.Status);

        var duplicateRes = await _client.PostAsync($"/api/invoices/{draft.Id}/duplicate", null);
        Assert.True(duplicateRes.IsSuccessStatusCode, await duplicateRes.Content.ReadAsStringAsync());
        var duplicate = await duplicateRes.Content.ReadFromJsonAsync<InvoiceResponse>(TestJson.Options);
        Assert.NotNull(duplicate);
        Assert.Equal(InvoiceStatus.Draft, duplicate.Status);
        Assert.NotEqual(draft.Id, duplicate.Id);

        var cancelRes = await _client.PostAsJsonAsync(
            $"/api/invoices/{duplicate.Id}/cancel",
            new CancelInvoiceRequest("test cancel"));
        Assert.True(cancelRes.IsSuccessStatusCode, await cancelRes.Content.ReadAsStringAsync());
        var cancelled = await cancelRes.Content.ReadFromJsonAsync<InvoiceStatusResponse>(TestJson.Options);
        Assert.NotNull(cancelled);
        Assert.Equal(InvoiceStatus.Cancelled, cancelled.Status);

        var logs = await _client.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            $"/api/logs?entityId={draft.Id}&pageSize=20",
            TestJson.Options);
        Assert.NotNull(logs);
        Assert.Contains(logs.Items, l => l.Action == AuditAction.Issued);
        Assert.Contains(logs.Items, l => l.Action == AuditAction.Paid);
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        await DemoAuth.LoginAsDemoAsync(_client);

        var page = await _client.GetFromJsonAsync<PagedResponse<InvoiceListItemResponse>>(
            "/api/invoices?status=Overdue&pageSize=50",
            TestJson.Options);
        Assert.NotNull(page);
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, i => Assert.Equal(InvoiceStatus.Overdue, i.Status));
    }
}
