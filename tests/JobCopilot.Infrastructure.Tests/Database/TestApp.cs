using JobCopilot.Application.Abstractions;
using JobCopilot.Application.Applications;
using JobCopilot.Application.Documents;
using JobCopilot.Application.JobAds;
using JobCopilot.Application.Profiles;
using JobCopilot.Domain.JobAds;
using JobCopilot.Domain.Matching;
using JobCopilot.Domain.Profiles;
using JobCopilot.Infrastructure.Pdf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace JobCopilot.Infrastructure.Tests.Database;

/// <summary>
/// The application services wired against a test database, with a controllable clock and a fake
/// job source – everything real except time and the network.
/// </summary>
public sealed class TestApp : IAsyncDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private TestApp(TestDatabase database)
    {
        Database = database;
        Matching = new MatchingService(database.Factory, Time);
        Profiles = new ProfileService(database.Factory, Matching, Time);
        Searches = new SavedSearchService(database.Factory, Time);
        Ads = new JobAdService(database.Factory, Matching, Time);
        Applications = new ApplicationService(database.Factory, Time);
        Export = new DocumentExportService(database.Factory, new QuestPdfRenderer());
        Ingestion = new JobAdIngestionService(
            database.Factory, Source, Matching, Time, NullLogger<JobAdIngestionService>.Instance);
    }

    public TestDatabase Database { get; }
    public FakeTimeProvider Time { get; } = new(Now);
    public FakeJobAdSource Source { get; } = new();
    public MatchingService Matching { get; }
    public ProfileService Profiles { get; }
    public SavedSearchService Searches { get; }
    public JobAdService Ads { get; }
    public ApplicationService Applications { get; }
    public DocumentExportService Export { get; }
    public JobAdIngestionService Ingestion { get; }

    public static async Task<TestApp> CreateAsync(PostgresFixture fixture) =>
        new(await fixture.CreateDatabaseAsync());

    public ValueTask DisposeAsync() => Database.DisposeAsync();

    public static MasterProfile Profile(params string[] skills) => new()
    {
        FullName = "Anna Andersson",
        Headline = "Junior .NET-utvecklare",
        Email = "anna@example.com",
        Skills = skills.Select(name => new Skill { Name = name, Category = "Teknik" }).ToList(),
    };

    public static JobAdContent Listing(string id, string headline = "Utvecklare", string description = "C# och Docker.") => new()
    {
        ExternalId = id,
        Headline = headline,
        EmployerName = "Bolaget AB",
        Municipality = "Norrköping",
        Description = description,
        PublishedAt = Now.AddDays(-1),
        ApplicationDeadline = Now.AddDays(14),
    };

    /// <summary>Stores an ad directly, with an explicit match so tests can control ranking.</summary>
    public async Task<JobAd> SeedAdAsync(
        string id,
        int score = 50,
        string headline = "Utvecklare",
        string description = "Text",
        string? municipality = "Norrköping",
        DateTimeOffset? deadline = null,
        DateTimeOffset? publishedAt = null,
        string[]? matched = null,
        string[]? missing = null,
        bool hidden = false)
    {
        var content = Listing(id, headline, description) with
        {
            Municipality = municipality,
            ApplicationDeadline = deadline,
            PublishedAt = publishedAt ?? Now.AddDays(-1),
        };

        var ad = JobAd.Create(JobAd.JobTechSource, content, Now);
        ad.ApplyMatch(new MatchResult(score, matched ?? [], missing ?? [], SeniorityHint.None), Now);
        if (hidden)
        {
            ad.Hide();
        }

        await using var db = Database.CreateContext();
        db.JobAds.Add(ad);
        await db.SaveChangesAsync();
        return ad;
    }
}

/// <summary>A job source that returns canned listings per query, or fails on demand.</summary>
public sealed class FakeJobAdSource : IJobAdSource
{
    private readonly Dictionary<string, IReadOnlyList<JobAdContent>> _results = [];
    private readonly HashSet<string> _failing = [];

    public List<JobSearchCriteria> Calls { get; } = [];

    public void Returns(string query, params JobAdContent[] listings) => _results[query] = listings;

    public void Fails(string query) => _failing.Add(query);

    public Task<IReadOnlyList<JobAdContent>> SearchAsync(JobSearchCriteria criteria, CancellationToken cancellationToken)
    {
        Calls.Add(criteria);

        if (_failing.Contains(criteria.Query))
        {
            throw new JobAdSourceException("JobTech answered 503 Service Unavailable");
        }

        return Task.FromResult(_results.GetValueOrDefault(criteria.Query, []));
    }
}
