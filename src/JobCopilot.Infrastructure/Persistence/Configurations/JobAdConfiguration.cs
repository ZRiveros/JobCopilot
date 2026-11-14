using JobCopilot.Domain.Applications;
using JobCopilot.Domain.JobAds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobCopilot.Infrastructure.Persistence.Configurations;

internal sealed class JobAdConfiguration : IEntityTypeConfiguration<JobAd>
{
    public void Configure(EntityTypeBuilder<JobAd> builder)
    {
        builder.HasKey(ad => ad.Id);

        // Ids are created in the domain (UUIDv7). Telling EF so makes it treat an entity it
        // discovers through a navigation as new instead of assuming it already exists.
        builder.Property(ad => ad.Id).ValueGeneratedNever();

        builder.Property(ad => ad.Source).HasMaxLength(50);
        builder.Property(ad => ad.ExternalId).HasMaxLength(100);
        builder.Property(ad => ad.Headline).HasMaxLength(500);
        builder.Property(ad => ad.EmployerName).HasMaxLength(300);
        builder.Property(ad => ad.Municipality).HasMaxLength(100);
        builder.Property(ad => ad.Region).HasMaxLength(100);
        builder.Property(ad => ad.WebpageUrl).HasMaxLength(2000);
        builder.Property(ad => ad.ApplicationUrl).HasMaxLength(2000);
        builder.Property(ad => ad.ApplicationEmail).HasMaxLength(320);
        builder.Property(ad => ad.EmploymentType).HasMaxLength(200);
        builder.Property(ad => ad.WorkingHours).HasMaxLength(200);
        builder.Property(ad => ad.WorkplaceModel).HasMaxLength(200);
        builder.Property(ad => ad.Occupation).HasMaxLength(200);
        builder.Property(ad => ad.Seniority).HasConversion<string>().HasMaxLength(20);

        // The same ad is found by several searches and on every fetch; this is the de-duplication key.
        builder.HasIndex(ad => new { ad.Source, ad.ExternalId }).IsUnique();
        builder.HasIndex(ad => ad.MatchScore);

        builder.HasOne(ad => ad.Application)
            .WithOne(application => application.JobAd)
            .HasForeignKey<JobApplication>(application => application.JobAdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SavedSearchConfiguration : IEntityTypeConfiguration<SavedSearch>
{
    public void Configure(EntityTypeBuilder<SavedSearch> builder)
    {
        builder.HasKey(search => search.Id);
        builder.Property(search => search.Id).ValueGeneratedNever();
        builder.Property(search => search.Name).HasMaxLength(200);
        builder.Property(search => search.Query).HasMaxLength(500);
        builder.Property(search => search.LastError).HasMaxLength(SavedSearch.MaxErrorLength);
    }
}
