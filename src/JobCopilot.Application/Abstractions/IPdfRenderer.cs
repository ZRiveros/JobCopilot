using JobCopilot.Application.Documents;

namespace JobCopilot.Application.Abstractions;

/// <summary>Turns a composed document into PDF bytes.</summary>
public interface IPdfRenderer
{
    byte[] RenderCv(CvDocument document);

    byte[] RenderCoverLetter(CoverLetterDocument document);
}
