using JobCopilot.Domain.JobAds;
using JobCopilot.Domain.Matching;
using JobCopilot.Domain.Profiles;

namespace JobCopilot.Application.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    public static MasterProfile Profile() => new()
    {
        FullName = "Anna Andersson",
        Headline = "Junior .NET-utvecklare",
        Email = "anna@example.com",
        Phone = "070-123 45 67",
        Location = "Norrköping",
        LinkedInUrl = "https://www.linkedin.com/in/anna-andersson/",
        GitHubUrl = "https://github.com/anna",
        Summary = "Nyfiken utvecklare som gillar backend.",
        Strengths = ["Noggrann", "Lär mig snabbt"],
        Skills =
        [
            new Skill { Name = "C#", Category = "Backend", Level = SkillLevel.Advanced },
            new Skill { Name = "Blazor", Category = "Frontend", Level = SkillLevel.Intermediate },
            new Skill { Name = "Git", Category = "", Level = SkillLevel.Basic },
        ],
        Experiences =
        [
            new Experience
            {
                Title = "Systemutvecklare (LIA)",
                Company = "Exempel AB",
                Location = "Linköping",
                StartDate = new DateOnly(2025, 1, 1),
                EndDate = new DateOnly(2025, 6, 30),
                Description = "Byggde ett internt verktyg.\nSkrev enhetstester.",
                Technologies = ["C#", "SQL Server"],
            },
        ],
        Projects =
        [
            new ProfileProject
            {
                Name = "JobCopilot",
                Description = "Självhostat verktyg för jobbsökande.",
                Technologies = ["Blazor", "PostgreSQL"],
            },
        ],
        Education =
        [
            new Education
            {
                School = "Yrkeshögskolan",
                Program = ".NET-utvecklare",
                StartDate = new DateOnly(2023, 8, 1),
                EndDate = new DateOnly(2025, 6, 1),
            },
        ],
        Languages = [new SpokenLanguage { Name = "Svenska", Level = "Modersmål" }],
    };

    public static JobAd Ad(string description = "Vi söker dig som kan C# och Docker.")
    {
        var ad = JobAd.Create(
            JobAd.JobTechSource,
            new JobAdContent
            {
                ExternalId = "1",
                Headline = "Backendutvecklare",
                EmployerName = "Bolaget AB",
                Municipality = "Norrköping",
                Description = description,
                EmploymentType = "Tillsvidareanställning",
            },
            Now);

        ad.ApplyMatch(new MatchResult(50, ["C#"], ["Docker"], SeniorityHint.None), Now);
        return ad;
    }
}
