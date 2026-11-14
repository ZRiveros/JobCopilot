using JobCopilot.Application.JobAds;
using JobCopilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobCopilot.Infrastructure.Tests.Persistence;

/// <summary>
/// Checks on the EF Core model that need no database: they only build the model and translate
/// queries to SQL, so they also run where Docker is not available.
/// </summary>
public class ModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=never_connected;Username=none;Password=none")
            .Options);

    [Fact]
    public void Migrations_AreInSyncWithTheModel()
    {
        using var db = CreateContext();

        // Fails when someone changes an entity or its configuration without adding a migration.
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void ProfileSections_AreStoredAsJsonAndSkillListsAsArrays()
    {
        using var db = CreateContext();

        var script = db.Database.GenerateCreateScript();

        Assert.Contains("\"Experiences\" jsonb", script, StringComparison.Ordinal);
        Assert.Contains("\"Skills\" jsonb", script, StringComparison.Ordinal);
        Assert.Contains("\"MissingSkills\" text[]", script, StringComparison.Ordinal);
        Assert.Contains("\"MunicipalityCodes\" text[]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AdFilter_WithEveryOptionSet_TranslatesToSql()
    {
        using var db = CreateContext();
        var filter = new JobAdFilter
        {
            Search = "Blazor",
            MinScore = 50,
            View = JobAdView.Saved,
            Municipality = "Norrköping",
            Skill = "Docker",
            IncludeExpired = false,
        };

        var sql = JobAdService.ApplyFilter(db.JobAds, filter, Now).ToQueryString();

        Assert.Contains("\"MatchScore\" >=", sql, StringComparison.Ordinal);
        Assert.Contains("\"Municipality\" =", sql, StringComparison.Ordinal);
        Assert.Contains("\"ApplicationDeadline\"", sql, StringComparison.Ordinal);
        Assert.Contains("lower(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ANY", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(JobAdView.Active)]
    [InlineData(JobAdView.Saved)]
    [InlineData(JobAdView.Hidden)]
    [InlineData(JobAdView.All)]
    public void AdFilter_EveryView_TranslatesToSql(JobAdView view)
    {
        using var db = CreateContext();

        var sql = JobAdService.ApplyFilter(db.JobAds, new JobAdFilter { View = view }, Now).ToQueryString();

        Assert.Contains("FROM \"JobAds\"", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(JobAdSort.BestMatch, "\"MatchScore\" DESC")]
    [InlineData(JobAdSort.Newest, "DESC")]
    [InlineData(JobAdSort.Deadline, "\"ApplicationDeadline\"")]
    public void AdSort_EveryOrder_TranslatesToSql(JobAdSort sort, string expectedInOrderBy)
    {
        using var db = CreateContext();

        var sql = JobAdService.ApplySort(db.JobAds, sort).ToQueryString();

        var orderBy = sql[sql.IndexOf("ORDER BY", StringComparison.Ordinal)..];
        Assert.Contains(expectedInOrderBy, orderBy, StringComparison.Ordinal);
    }
}
