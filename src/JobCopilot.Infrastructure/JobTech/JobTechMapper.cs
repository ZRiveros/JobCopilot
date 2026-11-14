using JobCopilot.Application.Common;
using JobCopilot.Domain.JobAds;

namespace JobCopilot.Infrastructure.JobTech;

internal static class JobTechMapper
{
    /// <summary>Maps a hit to ad content, or null when it lacks what an ad needs (id and headline).</summary>
    public static JobAdContent? ToContent(JobTechHit hit)
    {
        if (string.IsNullOrWhiteSpace(hit.Id) || string.IsNullOrWhiteSpace(hit.Headline) || hit.Removed == true)
        {
            return null;
        }

        var declaredSkills = (hit.MustHave?.Skills ?? [])
            .Concat(hit.NiceToHave?.Skills ?? [])
            .Select(skill => skill.Label)
            .OfType<string>()
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new JobAdContent
        {
            ExternalId = hit.Id.Trim(),
            Headline = Truncate(hit.Headline.Trim(), 500),
            EmployerName = Truncate(FirstNonBlank(hit.Employer?.Name, hit.Employer?.Workplace) ?? "", 300),
            Municipality = NullIfBlank(hit.WorkplaceAddress?.Municipality),
            Region = NullIfBlank(hit.WorkplaceAddress?.Region),
            Description = hit.Description?.Text?.Trim() ?? "",
            WebpageUrl = NullIfBlank(hit.WebpageUrl),
            ApplicationUrl = NullIfBlank(hit.ApplicationDetails?.Url),
            ApplicationEmail = NullIfBlank(hit.ApplicationDetails?.Email),
            // JobTech sends timestamps without offset, in Swedish local time.
            PublishedAt = hit.PublicationDate is { } published ? SwedishTime.ToUtc(published) : null,
            ApplicationDeadline = hit.ApplicationDeadline is { } deadline ? SwedishTime.ToUtc(deadline) : null,
            EmploymentType = NullIfBlank(hit.EmploymentType?.Label),
            WorkingHours = NullIfBlank(hit.WorkingHoursType?.Label),
            WorkplaceModel = NullIfBlank(hit.WorkplaceModel?.Label),
            Occupation = NullIfBlank(hit.Occupation?.Label),
            ExperienceRequired = hit.ExperienceRequired ?? false,
            DeclaredSkills = declaredSkills,
        };
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FirstNonBlank(params string?[] values) =>
        values.Select(NullIfBlank).FirstOrDefault(value => value is not null);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
