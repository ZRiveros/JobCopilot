using JobCopilot.Application.Abstractions;
using JobCopilot.Application.Common;
using JobCopilot.Domain.Applications;
using JobCopilot.Domain.Profiles;
using Microsoft.EntityFrameworkCore;

namespace JobCopilot.Application.Applications;

/// <summary>Use cases for the application board and the per-application workspace.</summary>
public sealed class ApplicationService(IAppDbContextFactory dbFactory, TimeProvider timeProvider)
{
    /// <summary>
    /// Saves an ad to the board. Saving twice is harmless: the existing card is returned.
    /// </summary>
    /// <returns>The application id, or null if the ad does not exist.</returns>
    public async Task<Guid?> SaveAdAsync(Guid jobAdId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);

        var existing = await db.Applications
            .Where(application => application.JobAdId == jobAdId)
            .Select(application => (Guid?)application.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var ad = await db.JobAds.FirstOrDefaultAsync(a => a.Id == jobAdId, cancellationToken);
        if (ad is null)
        {
            return null;
        }

        // Saving an ad is a clear sign it should not stay hidden.
        ad.Unhide();

        var application = JobApplication.CreateFor(jobAdId, timeProvider.GetUtcNow());
        db.Applications.Add(application);
        await db.SaveChangesAsync(cancellationToken);
        return application.Id;
    }

    public async Task<IReadOnlyList<ApplicationCard>> GetBoardAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        return await db.Applications
            .AsNoTracking()
            .OrderByDescending(application => application.StatusChangedAt)
            .Select(application => new ApplicationCard
            {
                Id = application.Id,
                JobAdId = application.JobAdId,
                Headline = application.JobAd.Headline,
                EmployerName = application.JobAd.EmployerName,
                Municipality = application.JobAd.Municipality,
                Deadline = application.JobAd.ApplicationDeadline,
                MatchScore = application.JobAd.MatchScore,
                Status = application.Status,
                StatusChangedAt = application.StatusChangedAt,
                AppliedOn = application.AppliedOn,
                InterviewOn = application.InterviewOn,
                FollowUpOn = application.FollowUpOn,
                ContactName = application.ContactName,
                VersionCount = application.Versions.Count,
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>Applications whose follow-up date is today or earlier, oldest first.</summary>
    public async Task<IReadOnlyList<ApplicationCard>> GetDueFollowUpsAsync(CancellationToken cancellationToken = default)
    {
        var today = SwedishTime.Today(timeProvider);
        var board = await GetBoardAsync(cancellationToken);
        return board
            .Where(card => card.FollowUpOn is { } date && date <= today)
            .OrderBy(card => card.FollowUpOn)
            .ToList();
    }

    /// <summary>Loads an application with its ad and all versions.</summary>
    public async Task<JobApplication?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        return await db.Applications
            .AsNoTracking()
            .Include(application => application.JobAd)
            .Include(application => application.Versions.OrderByDescending(version => version.Number))
            .FirstOrDefaultAsync(application => application.Id == id, cancellationToken);
    }

    public async Task MoveAsync(Guid id, ApplicationStatus status, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        var application = await db.Applications.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application is null)
        {
            return;
        }

        application.MoveTo(status, timeProvider.GetUtcNow(), SwedishTime.Today(timeProvider));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDetailsAsync(Guid id, ApplicationDetails details, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        var application = await db.Applications.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application is null)
        {
            return;
        }

        application.UpdateDetails(details);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Removes the card and all its versions. The ad itself stays in the feed.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        await db.Applications.Where(application => application.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Stores the documents as the next version of the application.</summary>
    /// <returns>The new version, or null if the application does not exist.</returns>
    public async Task<ApplicationVersion?> AddVersionAsync(
        Guid id, string cv, string coverLetter, string? label, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        var application = await db.Applications
            .Include(a => a.Versions)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var version = application.AddVersion(cv, coverLetter, label, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return version;
    }

    /// <summary>Builds the copy-paste prompt for an application from the current master profile.</summary>
    /// <returns>The prompt, or null if the application does not exist.</returns>
    public async Task<string?> BuildPromptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        var ad = await db.Applications.AsNoTracking()
            .Where(application => application.Id == id)
            .Select(application => application.JobAd)
            .FirstOrDefaultAsync(cancellationToken);
        if (ad is null)
        {
            return null;
        }

        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new MasterProfile();
        return ApplicationPromptBuilder.Build(profile, ad);
    }
}

/// <summary>A card on the application board.</summary>
public sealed record ApplicationCard
{
    public required Guid Id { get; init; }
    public required Guid JobAdId { get; init; }
    public required string Headline { get; init; }
    public required string EmployerName { get; init; }
    public string? Municipality { get; init; }
    public DateTimeOffset? Deadline { get; init; }
    public int MatchScore { get; init; }
    public ApplicationStatus Status { get; init; }
    public DateTimeOffset StatusChangedAt { get; init; }
    public DateOnly? AppliedOn { get; init; }
    public DateOnly? InterviewOn { get; init; }
    public DateOnly? FollowUpOn { get; init; }
    public string ContactName { get; init; } = "";
    public int VersionCount { get; init; }

    public bool IsFollowUpDue(DateOnly today) => FollowUpOn is { } date && date <= today;
}
