using JobCopilot.Domain.Matching;
using JobCopilot.Domain.Profiles;

namespace JobCopilot.Domain.Tests.Matching;

public class JobMatcherTests
{
    private static MasterProfile ProfileWith(params string[] skills) => new()
    {
        Skills = skills.Select(name => new Skill { Name = name }).ToList(),
    };

    [Fact]
    public void Match_AllSkillsCovered_Scores100()
    {
        var matcher = JobMatcher.ForProfile(ProfileWith("C#", ".NET", "SQL", "Git"));

        var result = matcher.Match("Systemutvecklare", "Vi använder C#, .NET, SQL och Git.");

        Assert.Equal(100, result.Score);
        Assert.Empty(result.MissingSkills);
        Assert.Equivalent(new[] { "C#", ".NET", "SQL", "Git" }, result.MatchedSkills, strict: true);
    }

    [Fact]
    public void Match_HalfOfEquallyWeightedSkills_Scores50()
    {
        var matcher = JobMatcher.ForProfile(ProfileWith("C#", ".NET"));

        var result = matcher.Match("Systemutvecklare", "Vi använder C#, .NET, Docker och Kubernetes.");

        Assert.Equal(50, result.Score);
        Assert.Equal(["Docker", "Kubernetes"], result.MissingSkills);
    }

    [Fact]
    public void Match_HeadlineSkillWeighsMoreThanBodySkill()
    {
        var matcher = JobMatcher.ForProfile(ProfileWith("Blazor"));

        // Blazor (headline, 3) matched; Docker + Azure (body, 1 each) missing => 3 / 5.
        var result = matcher.Match("Blazor-utvecklare", "Meriterande med Docker och Azure.");

        Assert.Equal(60, result.Score);
        Assert.Equal("Blazor", result.MatchedSkills[0]);
    }

    [Fact]
    public void Match_DeclaredSkillWeighsMoreThanBodySkill()
    {
        var matcher = JobMatcher.ForProfile(ProfileWith("Docker"));

        // Docker (body, 1) matched; Java (declared, 2) + Kubernetes (body, 1) missing => 1 / 4.
        var result = matcher.Match("Utvecklare", "Docker och Kubernetes.", ["Java, programmeringsspråk"]);

        Assert.Equal(25, result.Score);
        Assert.Equal(["Java", "Kubernetes"], result.MissingSkills);
    }

    [Fact]
    public void Match_SingleKnownSkill_CannotReachFullScore()
    {
        var matcher = JobMatcher.ForProfile(ProfileWith("C#"));

        var result = matcher.Match("Utvecklare", "Du kan C#.");

        Assert.Equal(25, result.Score);
    }

    [Fact]
    public void Match_NoKnownSkillsInAd_ScoresZero()
    {
        var matcher = JobMatcher.ForProfile(ProfileWith("C#"));

        var result = matcher.Match("Lagerarbetare", "Truckkort krävs.");

        Assert.Equal(0, result.Score);
        Assert.Empty(result.MatchedSkills);
        Assert.Empty(result.MissingSkills);
    }

    [Fact]
    public void Match_EmptyProfile_ReportsEverythingAsMissing()
    {
        var matcher = JobMatcher.ForProfile(new MasterProfile());

        var result = matcher.Match("C#-utvecklare", "C# och Azure.");

        Assert.Equal(0, result.Score);
        Assert.Equal(["C#", "Azure"], result.MissingSkills);
    }

    [Fact]
    public void Match_ProfileSkillWrittenAsAlias_StillMatches()
    {
        var matcher = JobMatcher.ForProfile(ProfileWith("csharp", "EF Core"));

        var result = matcher.Match("Utvecklare", "C# och Entity Framework.");

        Assert.Equivalent(new[] { "C#", "Entity Framework" }, result.MatchedSkills, strict: true);
    }

    [Fact]
    public void Match_ProfileSkillOutsideCatalog_IsRecognisedInAd()
    {
        var matcher = JobMatcher.ForProfile(ProfileWith("Fortran"));

        var result = matcher.Match("Utvecklare", "Vi underhåller Fortran-kod.");

        Assert.Equal(["Fortran"], result.MatchedSkills);
    }

    [Fact]
    public void Match_TechnologiesOnExperienceAndProjects_CountAsSkills()
    {
        var profile = new MasterProfile
        {
            Experiences = [new Experience { Title = "Utvecklare", Technologies = ["Docker"] }],
            Projects = [new ProfileProject { Name = "JobCopilot", Technologies = ["Blazor"] }],
        };

        var result = JobMatcher.ForProfile(profile).Match("Utvecklare", "Docker och Blazor.");

        Assert.Equivalent(new[] { "Docker", "Blazor" }, result.MatchedSkills, strict: true);
    }

    [Fact]
    public void Match_JuniorRole_GetsBonus()
    {
        var matcher = JobMatcher.ForProfile(ProfileWith("C#", ".NET"));

        var neutral = matcher.Match("Systemutvecklare", "C#, .NET, Docker och Azure.");
        var junior = matcher.Match("Junior systemutvecklare", "C#, .NET, Docker och Azure.");

        Assert.Equal(SeniorityHint.Junior, junior.Seniority);
        Assert.Equal(neutral.Score + JobMatcher.JuniorBonus, junior.Score);
    }

    [Fact]
    public void Match_SeniorRole_GetsPenalty()
    {
        var matcher = JobMatcher.ForProfile(ProfileWith("C#", ".NET"));

        var neutral = matcher.Match("Systemutvecklare", "C#, .NET, Docker och Azure.");
        var senior = matcher.Match("Senior systemutvecklare", "C#, .NET, Docker och Azure.");

        Assert.Equal(SeniorityHint.Senior, senior.Seniority);
        Assert.Equal(neutral.Score - JobMatcher.SeniorPenalty, senior.Score);
    }

    [Fact]
    public void Match_ScoreStaysWithinBounds()
    {
        var matcher = JobMatcher.ForProfile(ProfileWith("C#", ".NET", "SQL", "Git"));

        var result = matcher.Match("Junior utvecklare", "C#, .NET, SQL och Git.");

        Assert.Equal(100, result.Score);
    }

    [Theory]
    [InlineData("Junior .NET-utvecklare", "", SeniorityHint.Junior)]
    [InlineData("Juniorutvecklare till vårt team", "", SeniorityHint.Junior)]
    [InlineData("Nyexaminerad systemutvecklare", "", SeniorityHint.Junior)]
    [InlineData("Senior backendutvecklare", "", SeniorityHint.Senior)]
    [InlineData("Seniora utvecklare sökes", "", SeniorityHint.Senior)]
    [InlineData("Tech Lead", "", SeniorityHint.Senior)]
    [InlineData("Arkitekt inom .NET", "", SeniorityHint.Senior)]
    [InlineData("Systemutvecklare", "Du får stöd av seniora kollegor.", SeniorityHint.None)]
    [InlineData("Systemutvecklare", "Passar dig som är nyexaminerad.", SeniorityHint.Junior)]
    [InlineData("Junior eller senior utvecklare", "", SeniorityHint.Junior)]
    [InlineData("Systemutvecklare", "", SeniorityHint.None)]
    public void DetectSeniority_ReadsHeadlineFirst(string headline, string description, SeniorityHint expected)
    {
        Assert.Equal(expected, JobMatcher.DetectSeniority(headline, description));
    }
}
