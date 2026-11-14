using System.Net;
using System.Text;
using JobCopilot.Application.Abstractions;
using JobCopilot.Infrastructure.JobTech;
using Microsoft.Extensions.Options;

namespace JobCopilot.Infrastructure.Tests.JobTech;

public class JobTechClientTests
{
    private static readonly JobSearchCriteria Criteria = new(".NET", ["0581"], RemoteOnly: false);

    private static JobTechClient CreateClient(StubHandler handler, int pageSize = 100, int maxAds = 300)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://jobtech.test/") };
        var options = Options.Create(new JobTechOptions { PageSize = pageSize, MaxAdsPerSearch = maxAds });
        return new JobTechClient(http, options);
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>A page with <paramref name="count"/> minimal hits, numbered from <paramref name="firstId"/>.</summary>
    private static HttpResponseMessage Page(int total, int firstId, int count)
    {
        var hits = Enumerable.Range(firstId, count).Select(id => $$"""{"id":"{{id}}","headline":"Annons {{id}}"}""");
        return Json($$"""{"total":{"value":{{total}}},"hits":[{{string.Join(',', hits)}}]}""");
    }

    private static int QueryValue(Uri uri, string name) =>
        int.Parse(
            uri.Query.TrimStart('?').Split('&').Single(part => part.StartsWith(name + "=", StringComparison.Ordinal))[(name.Length + 1)..],
            System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task SearchAsync_MapsRealResponseShape()
    {
        var fixture = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "jobtech-search.json"));
        var client = CreateClient(new StubHandler(_ => Json(fixture)));

        var ads = await client.SearchAsync(Criteria, CancellationToken.None);

        Assert.Equal(["31629336", "31700001"], ads.Select(ad => ad.ExternalId));

        var ad = ads[0];
        Assert.Equal(".NET Developer", ad.Headline);
        Assert.Equal("Exempel Consulting AB", ad.EmployerName);
        Assert.Equal("Norrköping", ad.Municipality);
        Assert.Equal("Östergötlands län", ad.Region);
        Assert.StartsWith("Software Engineer (C# / .NET)", ad.Description, StringComparison.Ordinal);
        Assert.Equal("https://arbetsformedlingen.se/platsbanken/annonser/31629336", ad.WebpageUrl);
        Assert.Equal("https://example.com/apply/8586908", ad.ApplicationUrl);
        Assert.Equal("jobb@example.com", ad.ApplicationEmail);
        Assert.Equal("Tidsbegränsad anställning", ad.EmploymentType);
        Assert.Equal("Heltid", ad.WorkingHours);
        Assert.Equal("Arbete på plats", ad.WorkplaceModel);
        Assert.Equal("Systemutvecklare", ad.Occupation);
        Assert.True(ad.ExperienceRequired);
        Assert.Equal(["C#, programmeringsspråk", "Docker"], ad.DeclaredSkills);

        // JobTech sends Swedish local time without offset: winter time is UTC+1, summer time UTC+2.
        Assert.Equal(new DateTimeOffset(2026, 11, 28, 22, 59, 59, TimeSpan.Zero), ad.ApplicationDeadline);
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 22, 0, 0, TimeSpan.Zero), ad.PublishedAt);
    }

    [Fact]
    public async Task SearchAsync_SparseHit_FallsBackToSafeDefaults()
    {
        var fixture = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "jobtech-search.json"));
        var client = CreateClient(new StubHandler(_ => Json(fixture)));

        var ad = (await client.SearchAsync(Criteria, CancellationToken.None))[1];

        Assert.Equal("Kommunens IT-enhet", ad.EmployerName);
        Assert.Equal("", ad.Description);
        Assert.Null(ad.Municipality);
        Assert.Null(ad.ApplicationUrl);
        Assert.Null(ad.ApplicationDeadline);
        Assert.False(ad.ExperienceRequired);
        Assert.Empty(ad.DeclaredSkills);
        Assert.Equal(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero), ad.PublishedAt);
    }

    [Fact]
    public async Task SearchAsync_FollowsPagesUntilTotalIsReached()
    {
        var handler = new StubHandler(request =>
        {
            var offset = QueryValue(request.RequestUri!, "offset");
            return Page(total: 250, firstId: offset, count: Math.Min(100, 250 - offset));
        });

        var ads = await CreateClient(handler, maxAds: 1000).SearchAsync(Criteria, CancellationToken.None);

        Assert.Equal(250, ads.Count);
        Assert.Equal([0, 100, 200], handler.Requests.Select(uri => QueryValue(uri, "offset")));
    }

    [Fact]
    public async Task SearchAsync_StopsAtConfiguredMaximum()
    {
        var handler = new StubHandler(request =>
            Page(total: 5000, firstId: QueryValue(request.RequestUri!, "offset"), count: QueryValue(request.RequestUri!, "limit")));

        var ads = await CreateClient(handler, pageSize: 100, maxAds: 150).SearchAsync(Criteria, CancellationToken.None);

        Assert.Equal(150, ads.Count);
        Assert.Equal([100, 50], handler.Requests.Select(uri => QueryValue(uri, "limit")));
    }

    [Fact]
    public async Task SearchAsync_NoHits_ReturnsEmptyListAfterOneRequest()
    {
        var handler = new StubHandler(_ => Page(total: 0, firstId: 0, count: 0));

        var ads = await CreateClient(handler).SearchAsync(Criteria, CancellationToken.None);

        Assert.Empty(ads);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "500")]
    [InlineData(HttpStatusCode.TooManyRequests, "429")]
    [InlineData(HttpStatusCode.BadRequest, "400")]
    public async Task SearchAsync_ErrorStatus_ThrowsSourceExceptionWithStatus(HttpStatusCode status, string expectedInMessage)
    {
        var client = CreateClient(new StubHandler(_ => new HttpResponseMessage(status)));

        var exception = await Assert.ThrowsAsync<JobAdSourceException>(() => client.SearchAsync(Criteria, CancellationToken.None));

        Assert.Contains(expectedInMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_NetworkFailure_ThrowsSourceException()
    {
        var client = CreateClient(new StubHandler(_ => throw new HttpRequestException("No such host is known")));

        var exception = await Assert.ThrowsAsync<JobAdSourceException>(() => client.SearchAsync(Criteria, CancellationToken.None));

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task SearchAsync_Timeout_ThrowsSourceException()
    {
        // HttpClient reports its own timeout as a TaskCanceledException that the caller did not ask for.
        var client = CreateClient(new StubHandler(_ => throw new TaskCanceledException("The request timed out")));

        await Assert.ThrowsAsync<JobAdSourceException>(() => client.SearchAsync(Criteria, CancellationToken.None));
    }

    [Theory]
    [InlineData("<html>Bad gateway</html>")]
    [InlineData("{\"hits\": \"not a list\"}")]
    public async Task SearchAsync_UnreadableBody_ThrowsSourceException(string body)
    {
        var client = CreateClient(new StubHandler(_ => Json(body)));

        await Assert.ThrowsAsync<JobAdSourceException>(() => client.SearchAsync(Criteria, CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_CallerCancels_PropagatesCancellationInsteadOfWrapping()
    {
        using var cancellation = new CancellationTokenSource();
        var client = CreateClient(new StubHandler(_ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SearchAsync(Criteria, cancellation.Token));
    }

    [Fact]
    public void BuildUri_EncodesQueryAndRepeatsMunicipalities()
    {
        var uri = JobTechClient.BuildUri(new JobSearchCriteria("C# backend", ["0581", "0580"], RemoteOnly: true), offset: 100, limit: 50);

        Assert.Equal("search?q=C%23%20backend&municipality=0581&municipality=0580&remote=true&sort=pubdate-desc&offset=100&limit=50", uri);
    }

    [Fact]
    public void BuildUri_WithoutQueryOrFilters_OnlyHasPaging()
    {
        var uri = JobTechClient.BuildUri(new JobSearchCriteria("  ", [], RemoteOnly: false), offset: 0, limit: 100);

        Assert.Equal("search?sort=pubdate-desc&offset=0&limit=100", uri);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }
}
