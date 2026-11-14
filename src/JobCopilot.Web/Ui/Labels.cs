using JobCopilot.Application.Applications;
using JobCopilot.Application.JobAds;
using JobCopilot.Domain.Applications;
using JobCopilot.Domain.Matching;
using JobCopilot.Domain.Profiles;

namespace JobCopilot.Web.Ui;

/// <summary>Swedish display texts for domain values. The domain itself stays language-neutral.</summary>
public static class Labels
{
    public static IReadOnlyList<ApplicationStatus> BoardColumns { get; } =
    [
        ApplicationStatus.Saved,
        ApplicationStatus.Sent,
        ApplicationStatus.Interview,
        ApplicationStatus.Offer,
        ApplicationStatus.Rejected,
    ];

    public static string Of(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Saved => "Sparad",
        ApplicationStatus.Sent => "Skickad",
        ApplicationStatus.Interview => "Intervju",
        ApplicationStatus.Offer => "Erbjudande",
        ApplicationStatus.Rejected => "Avslag",
        _ => status.ToString(),
    };

    public static string Of(SkillLevel level) => level switch
    {
        SkillLevel.Basic => "Grundläggande",
        SkillLevel.Intermediate => "God",
        SkillLevel.Advanced => "Mycket god",
        _ => level.ToString(),
    };

    public static string Of(JobAdView view) => view switch
    {
        JobAdView.Active => "Aktuella",
        JobAdView.Saved => "Sparade",
        JobAdView.Hidden => "Dolda",
        JobAdView.All => "Alla",
        _ => view.ToString(),
    };

    public static string Of(JobAdSort sort) => sort switch
    {
        JobAdSort.BestMatch => "Bäst matchning",
        JobAdSort.Newest => "Nyast först",
        JobAdSort.Deadline => "Sista ansökningsdag",
        _ => sort.ToString(),
    };

    public static string? Of(SeniorityHint seniority) => seniority switch
    {
        SeniorityHint.Junior => "Junior-roll",
        SeniorityHint.Senior => "Senior-roll",
        _ => null,
    };

    public static string Of(SkillCategory category) => category switch
    {
        SkillCategory.Language => "Språk",
        SkillCategory.Framework => "Ramverk",
        SkillCategory.Frontend => "Frontend",
        SkillCategory.Data => "Data",
        SkillCategory.Cloud => "Moln",
        SkillCategory.DevOps => "DevOps",
        SkillCategory.Architecture => "Arkitektur",
        SkillCategory.Testing => "Test",
        SkillCategory.Practice => "Arbetssätt",
        SkillCategory.Tool => "Verktyg",
        _ => "Övrigt",
    };

    /// <summary>Explains to the user what is wrong with a pasted AI answer and how to fix it.</summary>
    public static string Of(ResponseParseError error) => error switch
    {
        ResponseParseError.Empty => "Klistra in svaret från AI-tjänsten först.",
        ResponseParseError.MissingCvMarker =>
            $"Hittar inte markören {ResponseMarkers.Cv}. Kontrollera att du kopierade hela svaret, eller be modellen svara igen i rätt format.",
        ResponseParseError.MissingLetterMarker =>
            $"Hittar inte markören {ResponseMarkers.CoverLetter}. Svaret verkar vara avklippt – be modellen fortsätta eller svara igen.",
        ResponseParseError.WrongOrder =>
            $"Markörerna kommer i fel ordning. {ResponseMarkers.Cv} ska stå före {ResponseMarkers.CoverLetter}.",
        ResponseParseError.EmptyCv => "CV-delen är tom.",
        ResponseParseError.EmptyLetter => "Brevdelen är tom.",
        _ => "Svaret kunde inte läsas in.",
    };
}
