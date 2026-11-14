namespace JobCopilot.Domain.Applications;

/// <summary>
/// An immutable snapshot of the CV and cover letter for one application. Editing never changes a
/// version; it produces the next one.
/// </summary>
public sealed class ApplicationVersion
{
    private ApplicationVersion()
    {
    }

    internal ApplicationVersion(
        Guid jobApplicationId, int number, string cv, string coverLetter, string? label, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7(now);
        JobApplicationId = jobApplicationId;
        Number = number;
        Cv = cv.Trim();
        CoverLetter = coverLetter.Trim();
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid JobApplicationId { get; private set; }

    /// <summary>1-based and increasing per application.</summary>
    public int Number { get; private set; }

    /// <summary>CV body in the Markdown subset described in the prompt.</summary>
    public string Cv { get; private set; } = "";

    public string CoverLetter { get; private set; } = "";
    public string? Label { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
