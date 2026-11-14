using JobCopilot.Application.Abstractions;
using JobCopilot.Domain.JobAds;
using Microsoft.EntityFrameworkCore;

namespace JobCopilot.Application.JobAds;

public sealed class SavedSearchService(IAppDbContextFactory dbFactory, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<SavedSearch>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        return await db.SavedSearches.AsNoTracking().OrderBy(search => search.CreatedAt).ToListAsync(cancellationToken);
    }

    /// <summary>Creates the search when <see cref="SavedSearchInput.Id"/> is null, otherwise updates it.</summary>
    /// <exception cref="ArgumentException">The search has neither a query nor a municipality.</exception>
    public async Task SaveAsync(SavedSearchInput input, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);

        if (input.Id is { } id)
        {
            var search = await db.SavedSearches.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (search is null)
            {
                return;
            }

            search.Update(input.Name, input.Query, input.MunicipalityCodes, input.RemoteOnly, input.IsEnabled);
        }
        else
        {
            var search = SavedSearch.Create(
                input.Name, input.Query, input.MunicipalityCodes, input.RemoteOnly, timeProvider.GetUtcNow());
            search.Update(input.Name, input.Query, input.MunicipalityCodes, input.RemoteOnly, input.IsEnabled);
            db.SavedSearches.Add(search);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        await db.SavedSearches.Where(search => search.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Adds a few starter searches the first time the app runs, so the feed is not empty.
    /// </summary>
    public async Task SeedDefaultsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateAsync(cancellationToken);
        if (await db.SavedSearches.AnyAsync(cancellationToken))
        {
            return;
        }

        string[] eastGothland = ["0581", "0580"]; // Norrköping, Linköping
        var now = timeProvider.GetUtcNow();

        db.SavedSearches.AddRange(
            SavedSearch.Create(".NET i Norrköping/Linköping", ".NET", eastGothland, remoteOnly: false, now),
            SavedSearch.Create("C# i Norrköping/Linköping", "C#", eastGothland, remoteOnly: false, now.AddTicks(1)),
            SavedSearch.Create("Blazor i hela Sverige", "Blazor", [], remoteOnly: false, now.AddTicks(2)),
            SavedSearch.Create(".NET på distans", ".NET", [], remoteOnly: true, now.AddTicks(3)));

        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record SavedSearchInput
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = "";
    public string Query { get; init; } = "";
    public IReadOnlyList<string> MunicipalityCodes { get; init; } = [];
    public bool RemoteOnly { get; init; }
    public bool IsEnabled { get; init; } = true;
}
