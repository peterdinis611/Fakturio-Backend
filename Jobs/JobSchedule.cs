namespace Fakturio.Api.Jobs;

/// <summary>Shared UTC scheduling helpers for daily background jobs.</summary>
public static class JobSchedule
{
    public static TimeSpan DelayUntilNextUtc(int hour, int minute = 0)
    {
        hour = Math.Clamp(hour, 0, 23);
        minute = Math.Clamp(minute, 0, 59);
        var now = DateTime.UtcNow;
        var next = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Utc);
        if (next <= now)
            next = next.AddDays(1);
        return next - now;
    }

    public static async Task<bool> DelaySafe(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
