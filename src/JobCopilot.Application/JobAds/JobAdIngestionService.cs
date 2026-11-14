using JobCopilot.Application.Abstractions;
using JobCopilot.Domain.JobAds;
using JobCopilot.Domain.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobCopilot.Application.JobAds;

/// <summary>
/// Runs every enabled saved search against the job source, stores new ads, refreshes known ones
/// and scores them. A failing search is recorded and skipped; it never stops the other searches.
/// </summary>
public sealed class JobAdIngestionService(
    IAppDbContextFactory dbFactory,
    IJobAdSource source,
    MatchingService matching,
    TimeProvider timeProvider,
    ILogger<JobAdIngestionService> logger)
{
    public async Task<IngestionSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        var searchIds = await GetEnabledSearchIdsAsync(cancellationToken);
        if (searchIds.Count == 0)
        {
            return IngestionSummary.Nothing;
        }

        var matcher = await matching.CreateMatcherAsync(cancellationToken);
        var summary = IngestionSummary.Nothing;

        foreach (var searchId in searchIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            summary += await RunSearchAsync(searchId, matcher, cancellationToken);
        }

        logger.LogInformation(
            "Job ad fetch finished: {Searches} searches, {Failed} failed, {Fetched} ads fetched, {New} new",
            summary.Searches, summary.FailedSearches, summary.FetchedAds, summary.NewAds);

        return summary;
    }

    private async Task<List<Guid>> GetEnabledSearchIdsAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        return await db.SavedSearches
            .Where(search => search.IsEnabled)
            .OrderBy(search => search.CreatedAt)
            .Select(search => search.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<IngestionSummary> RunSearchAsync(Guid searchId, JobMatcher matcher, CancellationToken cancellationToken)
    {
        // One context per search keeps a failure in one search from affecting the next.
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        var search = await db.SavedSearches.FirstOrDefaultAsync(s => s.Id == searchId, cancellationToken);
        if (search is null)
        {
            return IngestionSummary.Nothing;
        }

        var now = timeProvider.GetUtcNow();
        IngestionSummary result;

        try
        {
            var criteria = new JobSearchCriteria(search.Query, search.MunicipalityCodes, search.RemoteOnly);
            var listings = await source.SearchAsync(criteria, cancellationToken);
            var newCount = await UpsertAsync(db, listings, matcher, now, cancellationToken);

            search.RecordSuccess(now, listings.Count, newCount);
            result = new IngestionSummary(1, 0, listings.Count, newCount);
        }
        catch (JobAdSourceException ex)
        {
            logger.LogWarning(ex, "Search '{Search}' failed and will be retried at the next fetch", search.Name);
            search.RecordFailure(now, ex.Message);
            result = new IngestionSummary(1, 1, 0, 0);
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// De-duplicates on the source's ad id: within the batch, and against ads already stored by
    /// earlier fetches or other searches.
    /// </summary>
    private static async Task<int> UpsertAsync(
        IAppDbContext db,
        IReadOnlyList<JobAdContent> listings,
        JobMatcher matcher,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var unique = listings
            .Where(listing => !string.IsNullOrWhiteSpace(listing.ExternalId))
            .DistinctBy(listing => listing.ExternalId)
            .ToList();

        var externalIds = unique.Select(listing => listing.ExternalId).ToList();
        var existing = await db.JobAds
            .Where(ad => ad.Source == JobAd.JobTechSource && externalIds.Contains(ad.ExternalId))
            .ToDictionaryAsync(ad => ad.ExternalId, cancellationToken);

        var newCount = 0;
        foreach (var listing in unique)
        {
            if (existing.TryGetValue(listing.ExternalId, out var ad))
            {
                ad.Refresh(listing, now);
            }
            else
            {
                ad = JobAd.Create(JobAd.JobTechSource, listing, now);
                db.JobAds.Add(ad);
                newCount++;
            }

            MatchingService.Score(ad, matcher, now);
        }

        return newCount;
    }
}

public readonly record struct IngestionSummary(int Searches, int FailedSearches, int FetchedAds, int NewAds)
{
    public static IngestionSummary Nothing => default;

    public static IngestionSummary operator +(IngestionSummary a, IngestionSummary b) =>
        new(a.Searches + b.Searches, a.FailedSearches + b.FailedSearches, a.FetchedAds + b.FetchedAds, a.NewAds + b.NewAds);
}
