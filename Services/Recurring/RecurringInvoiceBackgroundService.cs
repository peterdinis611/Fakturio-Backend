using Fakturio.Api.Interfaces;

namespace Fakturio.Api.Services.Recurring;

/// <summary>Hourly job: materialize due recurring invoice templates into draft invoices.</summary>
public sealed class RecurringInvoiceBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringInvoiceBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Recurring invoice job started.");

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunAsync(stoppingToken);
            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var recurring = scope.ServiceProvider.GetRequiredService<IRecurringInvoiceService>();
            var result = await recurring.ProcessDueAsync(ct);
            if (result.GeneratedCount > 0 || result.SkippedCount > 0)
            {
                logger.LogInformation(
                    "Recurring invoices: generated {Generated}, skipped {Skipped}.",
                    result.GeneratedCount,
                    result.SkippedCount);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Recurring invoice job failed.");
        }
    }
}
