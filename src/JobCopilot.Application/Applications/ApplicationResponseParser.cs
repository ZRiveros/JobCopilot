using System.Text.RegularExpressions;

namespace JobCopilot.Application.Applications;

/// <summary>
/// Splits a pasted AI answer into CV and cover letter using the <see cref="ResponseMarkers"/>.
/// Tolerates what chat models commonly add around the markers: a preamble, code fences,
/// bold markers, extra spaces and a missing end marker.
/// </summary>
public static partial class ApplicationResponseParser
{
    public static ResponseParseResult Parse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return ResponseParseResult.Failure(ResponseParseError.Empty);
        }

        var text = response.Replace("\r\n", "\n", StringComparison.Ordinal);
        var markers = MarkerLine().Matches(text);

        var cv = FindFirst(markers, "CV");
        var letter = FindFirst(markers, "BREV");

        if (cv is null)
        {
            return ResponseParseResult.Failure(ResponseParseError.MissingCvMarker);
        }

        if (letter is null)
        {
            return ResponseParseResult.Failure(ResponseParseError.MissingLetterMarker);
        }

        if (letter.Index < cv.Index)
        {
            return ResponseParseResult.Failure(ResponseParseError.WrongOrder);
        }

        var end = markers.FirstOrDefault(m => IsMarker(m, "SLUT") && m.Index > letter.Index);

        var cvText = Clean(text[(cv.Index + cv.Length)..letter.Index]);
        var letterStart = letter.Index + letter.Length;
        var letterText = Clean(end is null ? text[letterStart..] : text[letterStart..end.Index]);

        if (cvText.Length == 0)
        {
            return ResponseParseResult.Failure(ResponseParseError.EmptyCv);
        }

        if (letterText.Length == 0)
        {
            return ResponseParseResult.Failure(ResponseParseError.EmptyLetter);
        }

        return ResponseParseResult.Success(cvText, letterText);
    }

    private static Match? FindFirst(MatchCollection markers, string name) =>
        markers.FirstOrDefault(m => IsMarker(m, name));

    private static bool IsMarker(Match match, string name) =>
        string.Equals(match.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>Trims the section and removes a code fence wrapped around it.</summary>
    private static string Clean(string section)
    {
        var lines = section.Trim().Split('\n').ToList();

        if (lines.Count > 0 && lines[0].TrimStart().StartsWith("```", StringComparison.Ordinal))
        {
            lines.RemoveAt(0);
        }

        if (lines.Count > 0 && lines[^1].Trim() == "```")
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines.Select(line => line.TrimEnd())).Trim();
    }

    // A marker on its own line, optionally wrapped in Markdown emphasis, quotes or heading marks:
    // "===CV===", "**=== CV ===**", "## ===BREV===", "`===SLUT===`".
    [GeneratedRegex(@"^[ \t>*_`#]*={2,}[ \t]*(?<name>CV|BREV|SLUT)[ \t]*={2,}[ \t*_`]*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MarkerLine();
}

public enum ResponseParseError
{
    None = 0,
    Empty,
    MissingCvMarker,
    MissingLetterMarker,
    WrongOrder,
    EmptyCv,
    EmptyLetter,
}

public sealed record ResponseParseResult
{
    private ResponseParseResult()
    {
    }

    public bool IsSuccess => Error == ResponseParseError.None;
    public ResponseParseError Error { get; private init; }
    public string Cv { get; private init; } = "";
    public string CoverLetter { get; private init; } = "";

    public static ResponseParseResult Success(string cv, string coverLetter) =>
        new() { Cv = cv, CoverLetter = coverLetter };

    public static ResponseParseResult Failure(ResponseParseError error) => new() { Error = error };
}
