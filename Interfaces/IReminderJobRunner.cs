namespace Fakturio.Api.Interfaces;

/// <summary>Runs one reminder-dispatch pass (scan + notify + optional mark-as-sent).</summary>
public interface IReminderJobRunner
{
    Task RunAsync(CancellationToken ct = default);
}
