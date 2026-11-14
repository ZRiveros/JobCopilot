namespace JobCopilot.Domain.Matching;

/// <summary>
/// A skill the matcher can recognise in ad text.
/// </summary>
/// <param name="Name">Canonical display name, e.g. "C#".</param>
/// <param name="Category">Grouping used in the skill-gap view.</param>
public sealed record SkillTerm(string Name, SkillCategory Category)
{
    /// <summary>Other spellings that mean the same skill, e.g. "csharp" for "C#".</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>
    /// When true the name and aliases only match with exact casing. Needed for terms that are
    /// also ordinary words, such as "REST" versus "the rest of the team".
    /// </summary>
    public bool MatchCase { get; init; }

    public IEnumerable<string> SearchTerms => Aliases.Prepend(Name);
}

public enum SkillCategory
{
    Language,
    Framework,
    Frontend,
    Data,
    Cloud,
    DevOps,
    Architecture,
    Testing,
    Practice,
    Tool,
    Other,
}
