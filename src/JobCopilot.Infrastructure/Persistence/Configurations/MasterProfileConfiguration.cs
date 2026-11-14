using JobCopilot.Domain.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobCopilot.Infrastructure.Persistence.Configurations;

internal sealed class MasterProfileConfiguration : IEntityTypeConfiguration<MasterProfile>
{
    public void Configure(EntityTypeBuilder<MasterProfile> builder)
    {
        builder.ToTable("MasterProfiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Id).ValueGeneratedNever();

        builder.Property(profile => profile.FullName).HasMaxLength(200);
        builder.Property(profile => profile.Headline).HasMaxLength(200);
        builder.Property(profile => profile.Email).HasMaxLength(320);
        builder.Property(profile => profile.Phone).HasMaxLength(50);
        builder.Property(profile => profile.Location).HasMaxLength(200);
        builder.Property(profile => profile.LinkedInUrl).HasMaxLength(500);
        builder.Property(profile => profile.GitHubUrl).HasMaxLength(500);
        builder.Property(profile => profile.WebsiteUrl).HasMaxLength(500);

        // The profile is one aggregate that is always read and written as a whole, so its
        // sections are stored as jsonb documents on the row instead of five child tables.
        builder.ComplexCollection(profile => profile.Experiences, section => section.ToJson());
        builder.ComplexCollection(profile => profile.Projects, section => section.ToJson());
        builder.ComplexCollection(profile => profile.Skills, section => section.ToJson());
        builder.ComplexCollection(profile => profile.Education, section => section.ToJson());
        builder.ComplexCollection(profile => profile.Languages, section => section.ToJson());
    }
}
