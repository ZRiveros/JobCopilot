using JobCopilot.Domain.Profiles;

namespace JobCopilot.Domain.Tests.Profiles;

public class MasterProfileTests
{
    [Fact]
    public void IsEmpty_NewProfile_IsTrue()
    {
        Assert.True(new MasterProfile().IsEmpty);
    }

    [Fact]
    public void IsEmpty_WithSkill_IsFalse()
    {
        var profile = new MasterProfile { Skills = [new Skill { Name = "C#" }] };

        Assert.False(profile.IsEmpty);
    }

    [Fact]
    public void Normalize_DropsBlankRowsAndTrims()
    {
        var profile = new MasterProfile
        {
            FullName = "  Test Testsson ",
            Strengths = ["Nyfiken", " ", "nyfiken", " Noggrann "],
            Skills = [new Skill { Name = " C# ", Category = " Backend " }, new Skill { Name = "  " }],
            Experiences =
            [
                new Experience { Title = "Utvecklare", Technologies = ["Docker", "", " docker "] },
                new Experience(),
            ],
            Projects = [new ProfileProject(), new ProfileProject { Name = "JobCopilot" }],
            Education = [new Education()],
            Languages = [new SpokenLanguage { Name = "Svenska" }, new SpokenLanguage()],
        };

        profile.Normalize();

        Assert.Equal("Test Testsson", profile.FullName);
        Assert.Equal(["Nyfiken", "Noggrann"], profile.Strengths);
        var skill = Assert.Single(profile.Skills);
        Assert.Equal("C#", skill.Name);
        Assert.Equal("Backend", skill.Category);
        var experience = Assert.Single(profile.Experiences);
        Assert.Equal(["Docker"], experience.Technologies);
        Assert.Single(profile.Projects);
        Assert.Empty(profile.Education);
        Assert.Single(profile.Languages);
    }

    [Fact]
    public void AllSkillNames_CombinesSkillsAndTechnologies()
    {
        var profile = new MasterProfile
        {
            Skills = [new Skill { Name = "C#" }],
            Experiences = [new Experience { Title = "Dev", Technologies = ["Docker"] }],
            Projects = [new ProfileProject { Name = "App", Technologies = ["Blazor", " "] }],
        };

        Assert.Equal(["C#", "Docker", "Blazor"], profile.AllSkillNames());
    }
}
