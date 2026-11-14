namespace JobCopilot.Domain.Applications;

/// <summary>The columns of the application board, in pipeline order.</summary>
public enum ApplicationStatus
{
    Saved = 0,
    Sent = 1,
    Interview = 2,
    Offer = 3,
    Rejected = 4,
}
