using JobCopilot.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobCopilot.Infrastructure.Persistence.Configurations;

internal sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.HasKey(application => application.Id);
        builder.Property(application => application.Id).ValueGeneratedNever();

        builder.Property(application => application.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(application => application.ContactName).HasMaxLength(200);
        builder.Property(application => application.ContactEmail).HasMaxLength(320);
        builder.Property(application => application.ContactPhone).HasMaxLength(50);

        // One card per ad.
        builder.HasIndex(application => application.JobAdId).IsUnique();

        builder.HasMany(application => application.Versions)
            .WithOne()
            .HasForeignKey(version => version.JobApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ApplicationVersionConfiguration : IEntityTypeConfiguration<ApplicationVersion>
{
    public void Configure(EntityTypeBuilder<ApplicationVersion> builder)
    {
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Id).ValueGeneratedNever();
        builder.Property(version => version.Label).HasMaxLength(200);
        builder.HasIndex(version => new { version.JobApplicationId, version.Number }).IsUnique();
    }
}
