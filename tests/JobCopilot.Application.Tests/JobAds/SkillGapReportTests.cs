using JobCopilot.Application.JobAds;

namespace JobCopilot.Application.Tests.JobAds;

public class SkillGapReportTests
{
    [Fact]
    public void From_CountsHowManyAdsMissEachSkill()
    {
        var report = SkillGapReport.From(
        [
            new AdSkills(["C#"], ["Docker", "Azure"]),
            new AdSkills(["C#", "SQL"], ["Docker"]),
            new AdSkills([], ["Docker", "Kubernetes"]),
            new AdSkills(["C#"], []),
        ]);

        Assert.Equal(4, report.AdCount);
        Assert.Equal(
            [new SkillCount("Docker", 3, 0.75), new SkillCount("Azure", 1, 0.25), new SkillCount("Kubernetes", 1, 0.25)],
            report.Missing);
        Assert.Equal(
            [new SkillCount("C#", 3, 0.75), new SkillCount("SQL", 1, 0.25)],
            report.Matched);
    }

    [Fact]
    public void From_SameSkillTwiceInOneAd_CountsOnce()
    {
        var report = SkillGapReport.From([new AdSkills([], ["Docker", "docker"])]);

        Assert.Equal(1, Assert.Single(report.Missing).AdCount);
    }

    [Fact]
    public void From_NoAds_ReturnsEmptyReport()
    {
        var report = SkillGapReport.From([]);

        Assert.Equal(0, report.AdCount);
        Assert.Empty(report.Missing);
        Assert.Empty(report.Matched);
    }
}
