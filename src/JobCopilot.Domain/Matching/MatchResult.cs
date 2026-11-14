namespace JobCopilot.Domain.Matching;

/// <param name="Score">0–100, higher is a better fit.</param>
/// <param name="MatchedSkills">Skills the ad asks for that the profile has, most important first.</param>
/// <param name="MissingSkills">Skills the ad asks for that the profile lacks, most important first.</param>
/// <param name="Seniority">Whether the ad reads as a junior or senior role.</param>
public sealed record MatchResult(
    int Score,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    SeniorityHint Seniority)
{
    public static MatchResult Empty { get; } = new(0, [], [], SeniorityHint.None);
}

public enum SeniorityHint
{
    None = 0,
    Junior = 1,
    Senior = 2,
}
