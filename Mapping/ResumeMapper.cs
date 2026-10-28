using Fakturio.Api.Dtos;
using Fakturio.Api.Models;

namespace Fakturio.Api.Mapping;

public static class ResumeMapper
{
    public static ResumeProfileResponse ToResponse(ResumeProfile p) => new(
        p.Id,
        p.FullName,
        p.Headline,
        p.Summary,
        p.Email,
        p.Phone,
        p.Location,
        p.Website,
        p.Skills,
        p.UpdatedAt);

    public static void Apply(ResumeProfile profile, UpdateResumeProfileRequest request)
    {
        if (request.FullName is not null)
            profile.FullName = request.FullName.Trim();
        if (request.Headline is not null)
            profile.Headline = request.Headline.Trim();
        if (request.Summary is not null)
            profile.Summary = request.Summary.Trim();
        if (request.Email is not null)
            profile.Email = request.Email.Trim();
        if (request.Phone is not null)
            profile.Phone = request.Phone.Trim();
        if (request.Location is not null)
            profile.Location = request.Location.Trim();
        if (request.Website is not null)
            profile.Website = request.Website.Trim();
        if (request.Skills is not null)
            profile.Skills = request.Skills.Trim();
        profile.UpdatedAt = DateTime.UtcNow;
    }

    public static ResumeEntryResponse ToResponse(ResumeEntry e) => new(
        e.Id,
        e.Type,
        e.Title,
        e.Organization,
        e.Location,
        e.StartDate,
        e.EndDate,
        e.IsCurrent,
        e.Description,
        e.SortOrder,
        e.CreatedAt,
        e.UpdatedAt);

    public static ResumeEntry FromCreate(CreateResumeEntryRequest request)
    {
        var isCurrent = request.IsCurrent ?? false;
        return new()
        {
            Type = request.Type.Trim().ToLowerInvariant(),
            Title = request.Title.Trim(),
            Organization = request.Organization?.Trim() ?? string.Empty,
            Location = request.Location?.Trim() ?? string.Empty,
            StartDate = request.StartDate,
            EndDate = isCurrent ? null : request.EndDate,
            IsCurrent = isCurrent,
            Description = request.Description?.Trim() ?? string.Empty,
            SortOrder = request.SortOrder ?? 0,
        };
    }

    public static void ApplyUpdate(ResumeEntry entry, UpdateResumeEntryRequest request)
    {
        if (request.Type is not null)
            entry.Type = request.Type.Trim().ToLowerInvariant();
        if (request.Title is not null)
            entry.Title = request.Title.Trim();
        if (request.Organization is not null)
            entry.Organization = request.Organization.Trim();
        if (request.Location is not null)
            entry.Location = request.Location.Trim();
        if (request.StartDate.HasValue)
            entry.StartDate = request.StartDate;
        if (request.IsCurrent.HasValue)
            entry.IsCurrent = request.IsCurrent.Value;
        if (request.EndDate.HasValue)
            entry.EndDate = request.EndDate;
        if (entry.IsCurrent)
            entry.EndDate = null;
        if (request.Description is not null)
            entry.Description = request.Description.Trim();
        if (request.SortOrder.HasValue)
            entry.SortOrder = request.SortOrder.Value;
        entry.UpdatedAt = DateTime.UtcNow;
    }
}
