namespace JobCopilot.Domain.Matching;

/// <summary>
/// The set of skills that can be recognised in text: the built-in catalog plus any skill from the
/// master profile that the catalog does not know about. Also maps aliases to one canonical name so
/// that "csharp" in the profile and "C#" in an ad are the same skill.
/// </summary>
public sealed class SkillLexicon
{
    private readonly List<SearchEntry> _entries = [];
    private readonly Dictionary<string, string> _canonicalByTerm = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SkillCategory> _categoryByCanonical = new(StringComparer.OrdinalIgnoreCase);

    private SkillLexicon()
    {
    }

    public static SkillLexicon Create(IEnumerable<SkillTerm> catalog, IEnumerable<string>? extraSkillNames = null)
    {
        var lexicon = new SkillLexicon();

        foreach (var term in catalog)
        {
            lexicon.Add(term);
        }

        foreach (var name in extraSkillNames ?? [])
        {
            var trimmed = name.Trim();
            if (trimmed.Length == 0 || lexicon._canonicalByTerm.ContainsKey(trimmed))
            {
                continue;
            }

            // Very short custom names ("R", "Go") would match all over the place if case was ignored.
            lexicon.Add(new SkillTerm(trimmed, SkillCategory.Other) { MatchCase = trimmed.Length <= 2 });
        }

        return lexicon;
    }

    /// <summary>Returns the canonical name for a skill, or the trimmed input when it is unknown.</summary>
    public string Canonicalize(string name)
    {
        var trimmed = name.Trim();
        return _canonicalByTerm.GetValueOrDefault(trimmed, trimmed);
    }

    public SkillCategory CategoryOf(string canonicalName) =>
        _categoryByCanonical.GetValueOrDefault(canonicalName, SkillCategory.Other);

    /// <summary>
    /// Returns the canonical names of all skills mentioned in <paramref name="text"/>.
    /// A mention that sits inside a longer mention of another skill is ignored, so
    /// "Azure DevOps" does not also count as "Azure" and "SQL Server" not as "SQL".
    /// </summary>
    public IReadOnlySet<string> Detect(string? text)
    {
        var detected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return detected;
        }

        var hits = new List<(TextSearch.Occurrence Span, string Canonical)>();
        foreach (var entry in _entries)
        {
            foreach (var occurrence in TextSearch.FindOccurrences(text, entry.Term, entry.MatchCase))
            {
                hits.Add((occurrence, entry.Canonical));
            }
        }

        foreach (var (span, canonical) in hits)
        {
            var shadowed = hits.Any(other =>
                other.Span.Length > span.Length
                && other.Span.Start <= span.Start
                && other.Span.End >= span.End
                && !string.Equals(other.Canonical, canonical, StringComparison.OrdinalIgnoreCase));

            if (!shadowed)
            {
                detected.Add(canonical);
            }
        }

        return detected;
    }

    private void Add(SkillTerm term)
    {
        _categoryByCanonical.TryAdd(term.Name, term.Category);

        foreach (var searchTerm in term.SearchTerms)
        {
            if (_canonicalByTerm.TryAdd(searchTerm, term.Name) || term.MatchCase)
            {
                _entries.Add(new SearchEntry(term.Name, searchTerm, term.MatchCase));
            }
        }
    }

    private sealed record SearchEntry(string Canonical, string Term, bool MatchCase);
}
