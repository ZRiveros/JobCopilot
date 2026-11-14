using JobCopilot.Domain.Profiles;

namespace JobCopilot.Domain.Matching;

/// <summary>
/// Scores a job ad against the master profile.
/// <para>
/// The score is deliberately simple and explainable: every skill found in the ad gets a weight
/// depending on where it was found, and the score is the share of that weight the profile covers.
/// </para>
/// <list type="bullet">
///   <item>Skill in the headline: weight <see cref="HeadlineWeight"/>.</item>
///   <item>Skill declared as a structured requirement by the employer: <see cref="DeclaredWeight"/>.</item>
///   <item>Skill mentioned in the body text: <see cref="BodyWeight"/>.</item>
/// </list>
/// The total weight has a floor (<see cref="MinimumTotalWeight"/>) so that an ad mentioning a
/// single known skill cannot reach 100 %. A junior/senior signal then nudges the score.
/// </summary>
public sealed class JobMatcher
{
    public const int HeadlineWeight = 3;
    public const int DeclaredWeight = 2;
    public const int BodyWeight = 1;
    public const int MinimumTotalWeight = 4;
    public const int JuniorBonus = 10;
    public const int SeniorPenalty = 15;

    private static readonly string[] JuniorTerms =
    [
        "junior", "nyexaminerad", "nyutexaminerad", "nyexad", "trainee", "graduate",
        "entry level", "entry-level", "early career", "i början av din karriär", "i början av karriären",
    ];

    private static readonly string[] SeniorTerms =
    [
        "senior", "lead", "principal", "arkitekt", "architect", "staff engineer", "expert", "erfaren",
    ];

    private readonly SkillLexicon _lexicon;
    private readonly HashSet<string> _profileSkills;

    private JobMatcher(SkillLexicon lexicon, HashSet<string> profileSkills)
    {
        _lexicon = lexicon;
        _profileSkills = profileSkills;
    }

    public static JobMatcher ForProfile(MasterProfile profile, IReadOnlyList<SkillTerm>? catalog = null)
    {
        var skillNames = profile.AllSkillNames().ToList();
        var lexicon = SkillLexicon.Create(catalog ?? SkillCatalog.Default, skillNames);
        var profileSkills = skillNames
            .Select(lexicon.Canonicalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new JobMatcher(lexicon, profileSkills);
    }

    public MatchResult Match(string headline, string description, IEnumerable<string>? declaredSkills = null)
    {
        var inHeadline = _lexicon.Detect(headline);
        var inBody = _lexicon.Detect(description);
        var declared = _lexicon.Detect(string.Join("\n", declaredSkills ?? []));

        var weights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        AddWeights(weights, inBody, BodyWeight);
        AddWeights(weights, declared, DeclaredWeight);
        AddWeights(weights, inHeadline, HeadlineWeight);

        var ordered = weights
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var matched = ordered.Where(pair => _profileSkills.Contains(pair.Key)).ToList();
        var missing = ordered.Where(pair => !_profileSkills.Contains(pair.Key)).ToList();

        var totalWeight = ordered.Sum(pair => pair.Value);
        var matchedWeight = matched.Sum(pair => pair.Value);
        var seniority = DetectSeniority(headline, description);

        var score = totalWeight == 0
            ? 0
            : (int)Math.Round(100.0 * matchedWeight / Math.Max(totalWeight, MinimumTotalWeight));

        if (score > 0)
        {
            score += seniority switch
            {
                SeniorityHint.Junior => JuniorBonus,
                SeniorityHint.Senior => -SeniorPenalty,
                _ => 0,
            };
        }

        return new MatchResult(
            Math.Clamp(score, 0, 100),
            matched.Select(pair => pair.Key).ToList(),
            missing.Select(pair => pair.Key).ToList(),
            seniority);
    }

    /// <summary>
    /// The headline is the strongest signal. "Senior" in body text is too noisy to use
    /// ("you will work with senior colleagues"), while junior wording in the body is rare
    /// enough to be meaningful.
    /// </summary>
    internal static SeniorityHint DetectSeniority(string headline, string description)
    {
        if (ContainsAny(headline, JuniorTerms))
        {
            return SeniorityHint.Junior;
        }

        if (ContainsAny(headline, SeniorTerms))
        {
            return SeniorityHint.Senior;
        }

        return ContainsAny(description, JuniorTerms) ? SeniorityHint.Junior : SeniorityHint.None;
    }

    private static bool ContainsAny(string text, string[] terms) =>
        terms.Any(term => TextSearch.Contains(text, term, matchCase: false, allowSuffix: IsInflected(term)));

    /// <summary>
    /// Terms that Swedish inflects or compounds ("seniora", "arkitekten", "juniorutvecklare"),
    /// so they are matched as word prefixes instead of whole words.
    /// </summary>
    private static bool IsInflected(string term) =>
        term is "junior" or "senior" or "arkitekt" or "architect" or "nyexaminerad" or "nyutexaminerad";

    private static void AddWeights(Dictionary<string, int> weights, IEnumerable<string> skills, int weight)
    {
        foreach (var skill in skills)
        {
            weights[skill] = Math.Max(weights.GetValueOrDefault(skill), weight);
        }
    }
}
