using JobCopilot.Application.Abstractions;
using JobCopilot.Application.JobAds;
using JobCopilot.Domain.Profiles;
using Microsoft.EntityFrameworkCore;

namespace JobCopilot.Application.Profiles;

public sealed class ProfileService(IAppDbContextFactory dbFactory, MatchingService matching, TimeProvider timeProvider)
{
    /// <summary>Returns the master profile, or an empty one if nothing has been saved yet.</summary>
    public async Task<MasterProfile> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        return await db.Profiles.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new MasterProfile();
    }

    /// <summary>
    /// Saves the profile and re-scores every ad, since the profile's skills decide the match.
    /// </summary>
    public async Task SaveAsync(MasterProfile profile, CancellationToken cancellationToken = default)
    {
        profile.Normalize();
        profile.UpdatedAt = timeProvider.GetUtcNow();

        await using (var db = await dbFactory.CreateAsync(cancellationToken))
        {
            if (await db.Profiles.AnyAsync(cancellationToken))
            {
                db.Profiles.Update(profile);
            }
            else
            {
                db.Profiles.Add(profile);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        await matching.RescoreAllAsync(cancellationToken);
    }
}
