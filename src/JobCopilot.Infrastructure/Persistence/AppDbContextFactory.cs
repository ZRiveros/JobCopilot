using JobCopilot.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobCopilot.Infrastructure.Persistence;

internal sealed class AppDbContextFactory(IDbContextFactory<AppDbContext> inner) : IAppDbContextFactory
{
    public async Task<IAppDbContext> CreateAsync(CancellationToken cancellationToken = default) =>
        await inner.CreateDbContextAsync(cancellationToken);
}

/// <summary>
/// Lets <c>dotnet ef</c> create the context without starting the web app. Adding a migration only
/// needs the model, not a reachable database, so the connection string is a placeholder.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=jobcopilot;Username=jobcopilot;Password=design-time-only")
            .Options;

        return new AppDbContext(options);
    }
}
