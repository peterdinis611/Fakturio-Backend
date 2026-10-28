namespace Fakturio.Api.Models;

public static class ResumeEntryTypes
{
    public const string Experience = "experience";
    public const string Education = "education";
    public const string Certification = "certification";
    public const string Language = "language";
    public const string Other = "other";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Experience,
        Education,
        Certification,
        Language,
        Other,
    };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());
}

public class ResumeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = ResumeEntryTypes.Experience;
    public string Title { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
