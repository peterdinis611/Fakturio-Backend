using FluentValidation;
using Fakturio.Api.Dtos;

namespace Fakturio.Api.Validation;

public sealed class UpdateJobSettingsRequestValidator : AbstractValidator<UpdateJobSettingsRequest>
{
    public UpdateJobSettingsRequestValidator()
    {
        RuleFor(x => x.OverdueSync).NotNull().SetValidator(new OverdueJobSettingsDtoValidator());
        RuleFor(x => x.ReminderJob).NotNull().SetValidator(new ReminderJobSettingsDtoValidator());
    }
}

public sealed class OverdueJobSettingsDtoValidator : AbstractValidator<OverdueJobSettingsDto>
{
    public OverdueJobSettingsDtoValidator()
    {
        RuleFor(x => x.RunAtHourUtc).InclusiveBetween(0, 23);
        RuleFor(x => x.RunAtMinuteUtc).InclusiveBetween(0, 59);
        RuleFor(x => x.StartupDelaySeconds).InclusiveBetween(0, 3600);
        RuleFor(x => x.DisabledRecheckHours).InclusiveBetween(1, 168);
    }
}

public sealed class ReminderJobSettingsDtoValidator : AbstractValidator<ReminderJobSettingsDto>
{
    public ReminderJobSettingsDtoValidator()
    {
        RuleFor(x => x.RunAtHourUtc).InclusiveBetween(0, 23);
        RuleFor(x => x.RunAtMinuteUtc).InclusiveBetween(0, 59);
        RuleFor(x => x.StartupDelaySeconds).InclusiveBetween(0, 3600);
        RuleFor(x => x.DisabledRecheckHours).InclusiveBetween(1, 168);
        RuleFor(x => x.WithinDays).InclusiveBetween(0, 90);
        RuleFor(x => x.MinDaysBetweenReminders).InclusiveBetween(0, 365);
    }
}
