using JobCopilot.Application.Abstractions;
using JobCopilot.Domain.Applications;
using JobCopilot.Domain.JobAds;
using JobCopilot.Domain.Profiles;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JobCopilot.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext, IDataProtectionKeyContext
{
    public DbSet<MasterProfile> Profiles => Set<MasterProfile>();
    public DbSet<JobAd> JobAds => Set<JobAd>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<ApplicationVersion> ApplicationVersions => Set<ApplicationVersion>();

    /// <summary>
    /// ASP.NET Core data-protection keys (they sign the auth cookie). Stored in the database so
    /// that logins survive container restarts without needing a volume for the web container.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
