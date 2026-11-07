using Fakturio.Api.Configuration;
using Fakturio.Api.Dtos;

namespace Fakturio.Api.Interfaces;

public interface IJobSettingsStore
{
    OverdueSyncOptions GetOverdue();
    ReminderJobOptions GetReminder();
    RecurringJobOptions GetRecurring();
    JobSettingsResponse GetSnapshot();
    Task<JobSettingsResponse> UpdateAsync(UpdateJobSettingsRequest request, CancellationToken ct = default);
    void RecordRun(string jobKey, string message);
}
