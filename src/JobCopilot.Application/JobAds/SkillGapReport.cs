namespace JobCopilot.Application.JobAds;

/// <summary>The matched and missing skills of one ad.</summary>
public sealed record AdSkills(IReadOnlyList<string> Matched, IReadOnlyList<string> Missing);

/// <param name="Skill">Canonical skill name.</param>
/// <param name="AdCount">Number of ads that mention the skill.</param>
/// <param name="Share">Fraction (0–1) of all analysed ads that mention the skill.</param>
public sealed record SkillCount(string Skill, int AdCount, double Share);

/// <summary>
/// Answers "what should I learn next?": which skills employers ask for most often that the
/// profile lacks, next to the ones it already covers.
/// </summary>
public sealed record SkillGapReport(int AdCount, IReadOnlyList<SkillCount> Missing, IReadOnlyList<SkillCount> Matched)
{
    public static SkillGapReport Empty { get; } = new(0, [], []);

    public static SkillGapReport From(IEnumerable<AdSkills> ads)
    {
        var list = ads.ToList();
        if (list.Count == 0)
        {
            return Empty;
        }

        return new SkillGapReport(
            list.Count,
            Count(list.Select(ad => ad.Missing), list.Count),
            Count(list.Select(ad => ad.Matched), list.Count));
    }

    private static List<SkillCount> Count(IEnumerable<IReadOnlyList<string>> skillLists, int adCount) =>
        skillLists
            .SelectMany(skills => skills.Distinct(StringComparer.OrdinalIgnoreCase))
            .GroupBy(skill => skill, StringComparer.OrdinalIgnoreCase)
            .Select(group => new SkillCount(group.Key, group.Count(), (double)group.Count() / adCount))
            .OrderByDescending(count => count.AdCount)
            .ThenBy(count => count.Skill, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
