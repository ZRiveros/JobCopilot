namespace JobCopilot.Domain.Matching;

/// <summary>
/// Finds whole-term occurrences in free text. Plain <c>\b</c> word boundaries do not work for
/// tech terms: "C#" and ".NET" start or end with punctuation, "Java" must not match "JavaScript",
/// "C" must not match "C#", and ".NET8" should still count as ".NET".
/// </summary>
internal static class TextSearch
{
    public readonly record struct Occurrence(int Start, int Length)
    {
        public int End => Start + Length;
    }

    public static bool Contains(string text, string term, bool matchCase = false, bool allowSuffix = false) =>
        FindOccurrences(text, term, matchCase, allowSuffix).Any();

    /// <param name="text">Text to search.</param>
    /// <param name="term">Term to look for.</param>
    /// <param name="matchCase">Require exact casing.</param>
    /// <param name="allowSuffix">
    /// Accept the term as a prefix of a longer word, e.g. "senior" in the Swedish "seniora".
    /// </param>
    public static IEnumerable<Occurrence> FindOccurrences(
        string text, string term, bool matchCase = false, bool allowSuffix = false)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
        {
            yield break;
        }

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var termStartsWithWordChar = char.IsLetterOrDigit(term[0]);
        var termEndsWithWordChar = char.IsLetterOrDigit(term[^1]);

        var index = 0;
        while (index <= text.Length - term.Length)
        {
            var start = text.IndexOf(term, index, comparison);
            if (start < 0)
            {
                yield break;
            }

            var end = start + term.Length;
            var startOk = !termStartsWithWordChar || start == 0 || !char.IsLetterOrDigit(text[start - 1]);
            var endOk = allowSuffix || end == text.Length || !ContinuesWord(text[end], termEndsWithWordChar);

            if (startOk && endOk)
            {
                yield return new Occurrence(start, term.Length);
            }

            index = start + 1;
        }
    }

    /// <summary>
    /// A following letter always means we are inside a longer word. Digits are allowed so that
    /// version suffixes ("HTML5", ".NET8") still match. '#' and '+' only continue a word when the
    /// term itself ends in a letter or digit – that is what separates "C" from "C#" and "C++".
    /// </summary>
    private static bool ContinuesWord(char next, bool termEndsWithWordChar) =>
        char.IsLetter(next) || (termEndsWithWordChar && next is '#' or '+');
}
