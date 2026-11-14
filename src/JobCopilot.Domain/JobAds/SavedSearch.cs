namespace JobCopilot.Domain.JobAds;

/// <summary>
/// A search that the background worker runs against the job source at every fetch.
/// </summary>
public sealed class SavedSearch
{
    public const int MaxErrorLength = 500;

    private SavedSearch()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";

    /// <summary>Free-text query, e.g. ".NET" or "C# backend".</summary>
    public string Query { get; private set; } = "";

    /// <summary>Four-digit municipality codes. Empty means the whole country.</summary>
    public List<string> MunicipalityCodes { get; private set; } = [];

    /// <summary>Only ads that the employer has marked as possible to do remotely.</summary>
    public bool RemoteOnly { get; private set; }

    public bool IsEnabled { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastRunAt { get; private set; }
    public bool? LastRunSucceeded { get; private set; }
    public string? LastError { get; private set; }
    public int LastResultCount { get; private set; }
    public int LastNewCount { get; private set; }

    public static SavedSearch Create(
        string name, string query, IEnumerable<string> municipalityCodes, bool remoteOnly, DateTimeOffset now)
    {
        var search = new SavedSearch { Id = Guid.CreateVersion7(now), CreatedAt = now };
        search.Update(name, query, municipalityCodes, remoteOnly, isEnabled: true);
        return search;
    }

    public void Update(string name, string query, IEnumerable<string> municipalityCodes, bool remoteOnly, bool isEnabled)
    {
        var codes = municipalityCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct()
            .ToList();

        if (string.IsNullOrWhiteSpace(query) && codes.Count == 0)
        {
            throw new ArgumentException("A search needs a query or at least one municipality.", nameof(query));
        }

        Query = query.Trim();
        Name = string.IsNullOrWhiteSpace(name) ? Query : name.Trim();
        MunicipalityCodes = codes;
        RemoteOnly = remoteOnly;
        IsEnabled = isEnabled;
    }

    public void RecordSuccess(DateTimeOffset now, int resultCount, int newCount)
    {
        LastRunAt = now;
        LastRunSucceeded = true;
        LastError = null;
        LastResultCount = resultCount;
        LastNewCount = newCount;
    }

    public void RecordFailure(DateTimeOffset now, string error)
    {
        LastRunAt = now;
        LastRunSucceeded = false;
        LastError = error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
    }
}
