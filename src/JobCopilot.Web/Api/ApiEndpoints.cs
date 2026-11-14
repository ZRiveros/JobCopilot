using JobCopilot.Application.Applications;
using JobCopilot.Application.Common;
using JobCopilot.Application.Documents;
using JobCopilot.Application.JobAds;
using JobCopilot.Domain.JobAds;
using JobCopilot.Domain.Matching;
using JobCopilot.Web.Fetching;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JobCopilot.Web.Api;

/// <summary>
/// A small JSON/file API next to the Blazor UI. The components talk to the application services
/// directly; these endpoints exist for what needs a real HTTP response (PDF downloads) and for
/// scripting against your own data. All of them require the login cookie (fallback policy).
/// </summary>
public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");

        api.MapGet("/ads", SearchAdsAsync);
        api.MapGet("/ads/{id:guid}", GetAdAsync);
        api.MapGet("/skill-gaps", (JobAdService ads, CancellationToken ct) => ads.GetSkillGapsAsync(ct));
        api.MapGet("/applications", (ApplicationService applications, CancellationToken ct) => applications.GetBoardAsync(ct));
        api.MapGet("/applications/versions/{versionId:guid}/cv.pdf",
            (Guid versionId, DocumentExportService export, CancellationToken ct) =>
                DownloadAsync(versionId, DocumentKind.Cv, export, ct));
        api.MapGet("/applications/versions/{versionId:guid}/letter.pdf",
            (Guid versionId, DocumentExportService export, CancellationToken ct) =>
                DownloadAsync(versionId, DocumentKind.CoverLetter, export, ct));

        // SameSite=Lax on the auth cookie keeps other sites from triggering this.
        api.MapPost("/fetch", (FetchCoordinator coordinator) =>
        {
            coordinator.RequestRun();
            return TypedResults.Accepted((string?)null);
        }).DisableAntiforgery();

        return endpoints;
    }

    private static Task<PagedResult<JobAdSummary>> SearchAdsAsync(
        [AsParameters] AdsQuery query, JobAdService ads, CancellationToken cancellationToken) =>
        ads.SearchAsync(query.ToFilter(), cancellationToken);

    private static async Task<Results<Ok<JobAdDetail>, NotFound>> GetAdAsync(
        Guid id, JobAdService ads, CancellationToken cancellationToken)
    {
        var ad = await ads.GetAsync(id, cancellationToken);
        return ad is null ? TypedResults.NotFound() : TypedResults.Ok(JobAdDetail.From(ad));
    }

    private static async Task<Results<FileContentHttpResult, NotFound>> DownloadAsync(
        Guid versionId, DocumentKind kind, DocumentExportService export, CancellationToken cancellationToken)
    {
        var document = await export.ExportAsync(versionId, kind, cancellationToken);
        return document is null
            ? TypedResults.NotFound()
            : TypedResults.File(document.Content, ExportedDocument.ContentType, document.FileName);
    }
}

/// <summary>Query-string parameters of <c>GET /api/ads</c>; all optional.</summary>
public sealed record AdsQuery(
    string? Search,
    int? MinScore,
    JobAdView? View,
    JobAdSort? Sort,
    string? Municipality,
    string? Skill,
    bool? IncludeExpired,
    int? Skip,
    int? Take)
{
    public JobAdFilter ToFilter() => new()
    {
        Search = Search,
        MinScore = MinScore ?? 0,
        View = View ?? JobAdView.Active,
        Sort = Sort ?? JobAdSort.BestMatch,
        Municipality = Municipality,
        Skill = Skill,
        IncludeExpired = IncludeExpired ?? false,
        Skip = Skip ?? 0,
        Take = Take ?? 30,
    };
}

/// <summary>The API shape of one ad. A DTO, so the entity graph is never serialised by accident.</summary>
public sealed record JobAdDetail(
    Guid Id,
    string Source,
    string Headline,
    string EmployerName,
    string? Municipality,
    string Description,
    string? WebpageUrl,
    string? ApplyUrl,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ApplicationDeadline,
    int MatchScore,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    SeniorityHint Seniority,
    bool IsHidden,
    Guid? ApplicationId)
{
    public static JobAdDetail From(JobAd ad) => new(
        ad.Id,
        ad.Source,
        ad.Headline,
        ad.EmployerName,
        ad.Municipality,
        ad.Description,
        ad.WebpageUrl,
        ad.ApplyUrl,
        ad.PublishedAt,
        ad.ApplicationDeadline,
        ad.MatchScore,
        ad.MatchedSkills,
        ad.MissingSkills,
        ad.Seniority,
        ad.IsHidden,
        ad.Application?.Id);
}
