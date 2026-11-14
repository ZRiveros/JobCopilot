using JobCopilot.Application.Abstractions;
using JobCopilot.Domain.JobAds;
using JobCopilot.Domain.Matching;
using JobCopilot.Domain.Profiles;
using Microsoft.EntityFrameworkCore;

namespace JobCopilot.Application.JobAds;

public sealed class MatchingService(IAppDbContextFactory dbFactory, TimeProvider timeProvider)
{
    private const int BatchSize = 200;

    /// <summary>Builds a matcher from the current master profile.</summary>
    public async Task<JobMatcher> CreateMatcherAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new MasterProfile();
        return JobMatcher.ForProfile(profile);
    }

    /// <summary>
    /// Re-scores every stored ad against the current profile. Works in batches with a fresh
    /// context each time so that memory stays flat no matter how many ads have accumulated.
    /// </summary>
    /// <returns>The number of ads scored.</returns>
    public async Task<int> RescoreAllAsync(CancellationToken cancellationToken = default)
    {
        var matcher = await CreateMatcherAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var total = 0;
        var lastId = Guid.Empty;

        while (true)
        {
            await using var db = await dbFactory.CreateAsync(cancellationToken);
            var cursor = lastId;
            var batch = await db.JobAds
                .Where(ad => ad.Id > cursor)
                .OrderBy(ad => ad.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                return total;
            }

            foreach (var ad in batch)
            {
                Score(ad, matcher, now);
            }

            await db.SaveChangesAsync(cancellationToken);
            total += batch.Count;
            lastId = batch[^1].Id;
        }
    }

    public static void Score(JobAd ad, JobMatcher matcher, DateTimeOffset now) =>
        ad.ApplyMatch(matcher.Match(ad.Headline, ad.Description, ad.DeclaredSkills), now);
}
