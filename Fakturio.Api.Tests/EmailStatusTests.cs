using System.Net;
using System.Net.Http.Json;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Tests.TestHelpers;

namespace Fakturio.Api.Tests;

[Collection(ApiFactoryCollection.Name)]
public sealed class EmailStatusTests
{
    private readonly HttpClient _client;

    public EmailStatusTests(FakturioApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Status_WhenProviderVirtual_ReturnsConfigured()
    {
        await DemoAuth.LoginAsDemoAsync(_client);

        var response = await _client.GetAsync("/api/emails/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await response.Content.ReadFromJsonAsync<EmailStatusResponse>(TestJson.Options);
        Assert.NotNull(status);
        Assert.True(status.Configured);
        Assert.Equal("Virtual", status.Provider);
        Assert.True(status.Simulated);
    }

    [Fact]
    public async Task SendTest_WhenVirtual_Succeeds()
    {
        await DemoAuth.LoginAsDemoAsync(_client);

        var response = await _client.PostAsJsonAsync(
            "/api/emails/test",
            new SendTestEmailRequest("demo@fakturio.sk"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SendTestEmailResponse>(TestJson.Options);
        Assert.NotNull(body);
        Assert.Equal("demo@fakturio.sk", body.To);
        Assert.Equal("Virtual", body.Provider);
    }

    [Fact]
    public async Task SendInvoiceEmail_WhenVirtual_WritesEmailSentAudit()
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
            Notes: "virtual-email",
            Status: InvoiceStatus.Draft,
            DocumentType: InvoiceDocumentType.Invoice,
            Flow: DocumentFlow.Issued,
            Items:
            [
                new CreateInvoiceItemRequest("Virtual email test", "ks", 1, 10m, 23m),
            ]);

        var createRes = await _client.PostAsJsonAsync("/api/invoices", create);
        Assert.True(createRes.IsSuccessStatusCode, await createRes.Content.ReadAsStringAsync());
        var draft = await createRes.Content.ReadFromJsonAsync<InvoiceResponse>(TestJson.Options);
        Assert.NotNull(draft);

        var issueRes = await _client.PatchAsJsonAsync(
            $"/api/invoices/{draft.Id}/status",
            new UpdateInvoiceStatusRequest(InvoiceStatus.Issued));
        Assert.True(issueRes.IsSuccessStatusCode, await issueRes.Content.ReadAsStringAsync());

        var sendRes = await _client.PostAsJsonAsync(
            $"/api/invoices/{draft.Id}/email",
            new SendInvoiceEmailRequest(
                "customer@example.com",
                null,
                "Virtuálny test",
                AttachPdf: false,
                IncludePublicLink: false));
        Assert.True(sendRes.IsSuccessStatusCode, await sendRes.Content.ReadAsStringAsync());

        var sent = await sendRes.Content.ReadFromJsonAsync<SendInvoiceEmailResponse>(TestJson.Options);
        Assert.NotNull(sent);
        Assert.Equal("customer@example.com", sent.To);
        Assert.Equal("Virtual", sent.Provider);

        var logs = await _client.GetFromJsonAsync<PagedResponse<AuditLogEntryResponse>>(
            $"/api/logs?entityId={draft.Id}&pageSize=20",
            TestJson.Options);
        Assert.NotNull(logs);
        Assert.Contains(logs.Items, l => l.Action == AuditAction.EmailSent);
    }
}
