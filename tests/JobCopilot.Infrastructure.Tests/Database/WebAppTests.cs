using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobCopilot.Application.Applications;
using JobCopilot.Application.JobAds;
using JobCopilot.Domain.JobAds;
using JobCopilot.Domain.Matching;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace JobCopilot.Infrastructure.Tests.Database;

/// <summary>
/// Starts the real web application (Program.cs) against a test database and drives it over HTTP:
/// start-up with migrations, the login wall, the API and PDF download.
/// </summary>
[Collection(DatabaseCollection.Name)]
public partial class WebAppTests(PostgresFixture fixture)
{
    private const string Password = "test-lösenord-123";

    [Fact]
    public async Task Startup_MigratesSeedsAndReportsHealthy()
    {
        await using var site = await TestSite.StartAsync(fixture);

        var response = await site.Client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = site.Services.CreateScope();
        var searches = await scope.ServiceProvider.GetRequiredService<SavedSearchService>().GetAllAsync();
        Assert.NotEmpty(searches);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/ansokningar")]
    [InlineData("/profil")]
    public async Task Pages_WithoutLogin_RedirectToLogin(string path)
    {
        await using var site = await TestSite.StartAsync(fixture);

        var response = await site.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location!.PathAndQuery, StringComparison.Ordinal);
        Assert.Contains("ReturnUrl=" + Uri.EscapeDataString(path), response.Headers.Location.Query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/api/ads")]
    [InlineData("/api/applications")]
    [InlineData("/api/skill-gaps")]
    public async Task Api_WithoutLogin_Returns401(string path)
    {
        await using var site = await TestSite.StartAsync(fixture);

        var response = await site.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginPage_IsReachableAndStyledWithoutLogin()
    {
        await using var site = await TestSite.StartAsync(fixture);

        var page = await site.Client.GetStringAsync("/login");

        Assert.Contains("Lösenord", page, StringComparison.Ordinal);
        var stylesheet = StylesheetHref().Match(page).Groups["href"].Value;
        Assert.Equal(HttpStatusCode.OK, (await site.Client.GetAsync("/" + stylesheet)).StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_IsRejected()
    {
        await using var site = await TestSite.StartAsync(fixture);

        var response = await site.LoginAsync("fel-lösenord");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=password", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, (await site.Client.GetAsync("/api/ads")).StatusCode);
    }

    [Fact]
    public async Task Login_WithoutAntiforgeryToken_IsRejected()
    {
        await using var site = await TestSite.StartAsync(fixture);

        var response = await site.Client.PostAsync(
            "/account/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["password"] = Password }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_CorrectPassword_OpensTheAppAndReturnsToTheRequestedPage()
    {
        await using var site = await TestSite.StartAsync(fixture);

        var response = await site.LoginAsync(Password, returnUrl: "/kompetensgap");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/kompetensgap", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await site.Client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await site.Client.GetAsync("/api/ads")).StatusCode);
    }

    [Fact]
    public async Task Login_ExternalReturnUrl_IsNotFollowed()
    {
        await using var site = await TestSite.StartAsync(fixture);

        var response = await site.LoginAsync(Password, returnUrl: "https://evil.example/");

        Assert.Equal("/", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        await using var site = await TestSite.StartAsync(fixture);
        await site.LoginAsync(Password);

        var response = await site.Client.PostAsync("/account/logout", content: null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await site.Client.GetAsync("/api/ads")).StatusCode);
    }

    [Fact]
    public async Task Api_Ads_ReturnsRankedAdsAsJson()
    {
        await using var site = await TestSite.StartAsync(fixture);
        await site.SeedAdAsync("1", "Låg", score: 20);
        await site.SeedAdAsync("2", "Hög", score: 80);
        await site.LoginAsync(Password);

        var page = await site.Client.GetFromJsonAsync<JsonElement>("/api/ads?minScore=10&sort=BestMatch");

        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        var first = page.GetProperty("items")[0];
        Assert.Equal("Hög", first.GetProperty("headline").GetString());
        Assert.Equal("None", first.GetProperty("seniority").GetString());

        var id = first.GetProperty("id").GetGuid();
        var detail = await site.Client.GetFromJsonAsync<JsonElement>($"/api/ads/{id}");
        Assert.Equal("Annonstext", detail.GetProperty("description").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await site.Client.GetAsync($"/api/ads/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Api_PdfDownload_ReturnsAttachment()
    {
        await using var site = await TestSite.StartAsync(fixture);
        var adId = await site.SeedAdAsync("1", "Backendutvecklare", score: 50);
        Guid versionId;
        using (var scope = site.Services.CreateScope())
        {
            var applications = scope.ServiceProvider.GetRequiredService<ApplicationService>();
            var applicationId = (await applications.SaveAdAsync(adId))!.Value;
            versionId = (await applications.AddVersionAsync(applicationId, "## Profil\nText", "Hej!", null))!.Id;
        }

        await site.LoginAsync(Password);
        var response = await site.Client.GetAsync($"/api/applications/versions/{versionId}/cv.pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 1000);

        var missing = await site.Client.GetAsync($"/api/applications/versions/{Guid.NewGuid()}/letter.pdf");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""(?<token>[^""]+)""")]
    private static partial Regex AntiforgeryToken();

    [GeneratedRegex(@"<link rel=""stylesheet"" href=""(?<href>app[^""]*.css)""")]
    private static partial Regex StylesheetHref();

    /// <summary>The running application plus an HTTP client that keeps cookies and does not follow redirects.</summary>
    private sealed class TestSite : IAsyncDisposable
    {
        private readonly TestDatabase _database;
        private readonly WebApplicationFactory<Program> _factory;

        private TestSite(TestDatabase database, WebApplicationFactory<Program> factory)
        {
            _database = database;
            _factory = factory;
            Client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => _factory.Services;

        public static async Task<TestSite> StartAsync(PostgresFixture fixture)
        {
            var database = await fixture.CreateDatabaseAsync();

            // Program.cs reads configuration while building the host, before WebApplicationFactory's
            // configuration hooks run, so the settings go in as environment variables. Tests in the
            // Database collection run one at a time, which makes that safe.
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", database.ConnectionString);
            Environment.SetEnvironmentVariable("Auth__Password", Password);
            Environment.SetEnvironmentVariable("Fetch__Enabled", "false");

            return new TestSite(database, new WebApplicationFactory<Program>());
        }

        public async Task<HttpResponseMessage> LoginAsync(string password, string? returnUrl = null)
        {
            var page = await Client.GetStringAsync("/login");
            var token = AntiforgeryToken().Match(page).Groups["token"].Value;
            Assert.False(string.IsNullOrEmpty(token), "The login page did not contain an antiforgery token.");

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token),
                ["password"] = password,
            };
            if (returnUrl is not null)
            {
                form["returnUrl"] = returnUrl;
            }

            return await Client.PostAsync("/account/login", new FormUrlEncodedContent(form));
        }

        public async Task<Guid> SeedAdAsync(string externalId, string headline, int score)
        {
            var ad = JobAd.Create(
                JobAd.JobTechSource,
                new JobAdContent { ExternalId = externalId, Headline = headline, EmployerName = "Bolaget AB", Description = "Annonstext" },
                TestApp.Now);
            ad.ApplyMatch(new MatchResult(score, [], [], SeniorityHint.None), TestApp.Now);

            await using var db = _database.CreateContext();
            db.JobAds.Add(ad);
            await db.SaveChangesAsync();
            return ad.Id;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _factory.DisposeAsync();
            await _database.DisposeAsync();
        }
    }
}
