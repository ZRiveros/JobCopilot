using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JobCopilot.Application.Abstractions;
using JobCopilot.Domain.JobAds;
using Microsoft.Extensions.Options;
using Polly;

namespace JobCopilot.Infrastructure.JobTech;

/// <summary>
/// Reads ads from the JobTech JobSearch API (Arbetsförmedlingen's open data, no API key).
/// Retries, timeouts and circuit breaking are handled by the resilience pipeline on the
/// HttpClient; whatever still fails is translated to <see cref="JobAdSourceException"/>.
/// </summary>
internal sealed class JobTechClient(HttpClient http, IOptions<JobTechOptions> options) : IJobAdSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly JobTechOptions _options = options.Value;

    public async Task<IReadOnlyList<JobAdContent>> SearchAsync(JobSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var ads = new List<JobAdContent>();
        var offset = 0;

        while (ads.Count < _options.MaxAdsPerSearch && offset <= JobTechOptions.MaxOffset)
        {
            var limit = Math.Min(_options.PageSize, _options.MaxAdsPerSearch - ads.Count);
            var page = await GetPageAsync(criteria, offset, limit, cancellationToken);
            var hits = page.Hits ?? [];

            ads.AddRange(hits.Select(JobTechMapper.ToContent).OfType<JobAdContent>());
            offset += hits.Count;

            var isLastPage = hits.Count < limit || offset >= (page.Total?.Value ?? 0);
            if (isLastPage)
            {
                break;
            }
        }

        return ads;
    }

    private async Task<JobTechSearchResponse> GetPageAsync(
        JobSearchCriteria criteria, int offset, int limit, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(BuildUri(criteria, offset, limit), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new JobAdSourceException(
                    $"JobTech answered {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd());
            }

            return await response.Content.ReadFromJsonAsync<JobTechSearchResponse>(JsonOptions, cancellationToken)
                ?? throw new JobAdSourceException("JobTech returned an empty response");
        }
        catch (HttpRequestException ex)
        {
            throw new JobAdSourceException("Could not reach JobTech: " + ex.Message, ex);
        }
        catch (ExecutionRejectedException ex)
        {
            // Polly gave up: total timeout exceeded or the circuit breaker is open.
            throw new JobAdSourceException("JobTech is not responding: " + ex.Message, ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new JobAdSourceException("The request to JobTech timed out", ex);
        }
        catch (JsonException ex)
        {
            throw new JobAdSourceException("JobTech returned a response that could not be read", ex);
        }
    }

    internal static string BuildUri(JobSearchCriteria criteria, int offset, int limit)
    {
        var query = new StringBuilder("search?");

        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            query.Append("q=").Append(Uri.EscapeDataString(criteria.Query.Trim())).Append('&');
        }

        foreach (var code in criteria.MunicipalityCodes)
        {
            query.Append("municipality=").Append(Uri.EscapeDataString(code)).Append('&');
        }

        if (criteria.RemoteOnly)
        {
            query.Append("remote=true&");
        }

        query.Append("sort=pubdate-desc")
            .Append("&offset=").Append(offset.ToString(CultureInfo.InvariantCulture))
            .Append("&limit=").Append(limit.ToString(CultureInfo.InvariantCulture));

        return query.ToString();
    }
}
