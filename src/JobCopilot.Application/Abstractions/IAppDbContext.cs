using JobCopilot.Domain.Applications;
using JobCopilot.Domain.JobAds;
using JobCopilot.Domain.Profiles;
using Microsoft.EntityFrameworkCore;

namespace JobCopilot.Application.Abstractions;

/// <summary>
/// The persistence port. Implemented by the EF Core context in Infrastructure.
/// </summary>
public interface IAppDbContext : IAsyncDisposable
{
    DbSet<MasterProfile> Profiles { get; }
    DbSet<JobAd> JobAds { get; }
    DbSet<SavedSearch> SavedSearches { get; }
    DbSet<JobApplication> Applications { get; }
    DbSet<ApplicationVersion> ApplicationVersions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates a short-lived context per operation. In Blazor Server a scoped context would live as
/// long as the browser tab, so services never hold on to one.
/// </summary>
public interface IAppDbContextFactory
{
    Task<IAppDbContext> CreateAsync(CancellationToken cancellationToken = default);
}
