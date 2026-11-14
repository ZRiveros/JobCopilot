using JobCopilot.Application.Abstractions;
using JobCopilot.Application.Common;
using JobCopilot.Domain.JobAds;
using Microsoft.EntityFrameworkCore;

namespace JobCopilot.Application.JobAds;

/// <summary>Reads and user actions for the ad feed.</summary>
public sealed class JobAdService(IAppDbContextFactory dbFactory, MatchingService matching, TimeProvider timeProvider)
{
    public const int MaxPageSize = 100;

    public async Task<PagedResult<JobAdSummary>> SearchAsync(JobAdFilter filter, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        var query = ApplyFilter(db.JobAds.AsNoTracking(), filter, timeProvider.GetUtcNow());

        var total = await query.CountAsync(cancellationToken);

        var items = await ApplySort(query, filter.Sort)
            .Skip(Math.Max(filter.Skip, 0))
            .Take(Math.Clamp(filter.Take, 1, MaxPageSize))
            .Select(ad => new JobAdSummary
            {
                Id = ad.Id,
                Headline = ad.Headline,
                EmployerName = ad.EmployerName,
                Municipality = ad.Municipality,
                WorkplaceModel = ad.WorkplaceModel,
                PublishedAt = ad.PublishedAt,
                ApplicationDeadline = ad.ApplicationDeadline,
                FirstSeenAt = ad.FirstSeenAt,
                Source = ad.Source,
                MatchScore = ad.MatchScore,
                MatchedSkills = ad.MatchedSkills,
                MissingSkills = ad.MissingSkills,
                Seniority = ad.Seniority,
                IsHidden = ad.IsHidden,
                ApplicationId = ad.Application == null ? null : ad.Application.Id,
                ApplicationStatus = ad.Application == null ? null : ad.Application.Status,
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<JobAdSummary>(items, total);
    }

    public async Task<JobAd?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        return await db.JobAds
            .AsNoTracking()
            .Include(ad => ad.Application)
            .FirstOrDefaultAsync(ad => ad.Id == id, cancellationToken);
    }

    /// <summary>The municipalities that occur among stored ads, for the filter drop-down.</summary>
    public async Task<IReadOnlyList<string>> GetMunicipalitiesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        return await db.JobAds
            .Where(ad => ad.Municipality != null && !ad.IsHidden)
            .Select(ad => ad.Municipality!)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

    public async Task SetHiddenAsync(Guid id, bool hidden, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        var ad = await db.JobAds.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (ad is null)
        {
            return;
        }

        if (hidden)
        {
            ad.Hide();
        }
        else
        {
            ad.Unhide();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Adds an ad found outside JobTech (LinkedIn, a company site) and scores it.</summary>
    public async Task<Guid> AddManualAsync(ManualJobAdInput input, CancellationToken cancellationToken = default)
    {
        var matcher = await matching.CreateMatcherAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var ad = JobAd.CreateManual(input.Headline, input.EmployerName, input.Municipality, input.Description, input.Url, now);
        MatchingService.Score(ad, matcher, now);

        await using var db = await dbFactory.CreateAsync(cancellationToken);
        db.JobAds.Add(ad);
        await db.SaveChangesAsync(cancellationToken);
        return ad.Id;
    }

    /// <summary>
    /// Counts how often each skill is missing (and matched) across the ads that are still
    /// relevant: not hidden and not past their deadline.
    /// </summary>
    public async Task<SkillGapReport> GetSkillGapsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var rows = await db.JobAds
            .AsNoTracking()
            .Where(ad => !ad.IsHidden && (ad.ApplicationDeadline == null || ad.ApplicationDeadline >= now))
            .Select(ad => new { ad.MatchedSkills, ad.MissingSkills })
            .ToListAsync(cancellationToken);

        return SkillGapReport.From(rows.Select(row => new AdSkills(row.MatchedSkills, row.MissingSkills)));
    }

    internal static IOrderedQueryable<JobAd> ApplySort(IQueryable<JobAd> query, JobAdSort sort) => sort switch
    {
        JobAdSort.Newest => query
            .OrderByDescending(ad => ad.PublishedAt ?? ad.FirstSeenAt)
            .ThenBy(ad => ad.Id),

        // Soonest deadline first; ads without a deadline last.
        JobAdSort.Deadline => query
            .OrderBy(ad => ad.ApplicationDeadline == null)
            .ThenBy(ad => ad.ApplicationDeadline)
            .ThenBy(ad => ad.Id),

        _ => query
            .OrderByDescending(ad => ad.MatchScore)
            .ThenByDescending(ad => ad.PublishedAt ?? ad.FirstSeenAt)
            .ThenBy(ad => ad.Id),
    };

    internal static IQueryable<JobAd> ApplyFilter(IQueryable<JobAd> query, JobAdFilter filter, DateTimeOffset now)
    {
        query = filter.View switch
        {
            JobAdView.Active => query.Where(ad => !ad.IsHidden),
            JobAdView.Saved => query.Where(ad => ad.Application != null),
            JobAdView.Hidden => query.Where(ad => ad.IsHidden),
            _ => query,
        };

        if (!filter.IncludeExpired)
        {
            query = query.Where(ad => ad.ApplicationDeadline == null || ad.ApplicationDeadline >= now);
        }

        if (filter.MinScore > 0)
        {
            query = query.Where(ad => ad.MatchScore >= filter.MinScore);
        }

        if (!string.IsNullOrWhiteSpace(filter.Municipality))
        {
            query = query.Where(ad => ad.Municipality == filter.Municipality);
        }

        if (!string.IsNullOrWhiteSpace(filter.Skill))
        {
            var skill = filter.Skill.Trim();
            query = query.Where(ad => ad.MatchedSkills.Contains(skill) || ad.MissingSkills.Contains(skill));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
#pragma warning disable CA1862 // EF Core translates ToLower().Contains(); the StringComparison overloads are not translatable.
            var term = filter.Search.Trim().ToLower(SwedishTime.Culture);
            query = query.Where(ad =>
                ad.Headline.ToLower().Contains(term)
                || ad.EmployerName.ToLower().Contains(term)
                || ad.Description.ToLower().Contains(term));
#pragma warning restore CA1862
        }

        return query;
    }
}
