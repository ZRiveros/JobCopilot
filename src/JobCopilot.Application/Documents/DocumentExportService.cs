using JobCopilot.Application.Abstractions;
using JobCopilot.Application.Common;
using JobCopilot.Domain.Profiles;
using Microsoft.EntityFrameworkCore;

namespace JobCopilot.Application.Documents;

public sealed class DocumentExportService(IAppDbContextFactory dbFactory, IPdfRenderer renderer)
{
    /// <summary>Renders a saved version as PDF, or returns null if the version does not exist.</summary>
    public async Task<ExportedDocument?> ExportAsync(Guid versionId, DocumentKind kind, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);

        var version = await db.ApplicationVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        if (version is null)
        {
            return null;
        }

        var ad = await db.Applications.AsNoTracking()
            .Where(application => application.Id == version.JobApplicationId)
            .Select(application => application.JobAd)
            .FirstAsync(cancellationToken);

        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new MasterProfile();

        var content = kind switch
        {
            DocumentKind.Cv => renderer.RenderCv(DocumentComposer.ComposeCv(profile, version.Cv)),
            _ => renderer.RenderCoverLetter(DocumentComposer.ComposeCoverLetter(
                profile, ad, version.CoverLetter, SwedishTime.ToLocalDate(version.CreatedAt))),
        };

        return new ExportedDocument(DocumentComposer.FileName(kind, profile, ad), content);
    }
}
