using JobCopilot.Domain.JobAds;

namespace JobCopilot.Application.Abstractions;

/// <summary>A source of job ads, such as the JobTech JobSearch API.</summary>
public interface IJobAdSource
{
    /// <exception cref="JobAdSourceException">The source could not be reached or answered with an error.</exception>
    Task<IReadOnlyList<JobAdContent>> SearchAsync(JobSearchCriteria criteria, CancellationToken cancellationToken);
}

/// <param name="Query">Free-text query; may be empty when municipalities are given.</param>
/// <param name="MunicipalityCodes">Four-digit municipality codes; empty means the whole country.</param>
/// <param name="RemoteOnly">Only ads marked as possible to do remotely.</param>
public sealed record JobSearchCriteria(string Query, IReadOnlyList<string> MunicipalityCodes, bool RemoteOnly);

/// <summary>
/// A failure to fetch ads that is expected to happen now and then (network, timeout, bad
/// response). The ingestion records it on the search and moves on.
/// </summary>
public sealed class JobAdSourceException : Exception
{
    public JobAdSourceException(string message)
        : base(message)
    {
    }

    public JobAdSourceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
