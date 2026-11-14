using JobCopilot.Application.JobAds;
using Microsoft.Extensions.Options;

namespace JobCopilot.Web.Fetching;

/// <summary>
/// Fetches job ads on a schedule: once shortly after start-up, then every
/// <see cref="FetchOptions.IntervalMinutes"/>, and whenever the UI asks through the
/// <see cref="FetchCoordinator"/>. A failed run is logged and the loop carries on.
/// </summary>
public sealed class JobAdFetchWorker(
    IServiceScopeFactory scopeFactory,
    FetchCoordinator coordinator,
    IOptions<FetchOptions> options,
    TimeProvider timeProvider,
    ILogger<JobAdFetchWorker> logger) : BackgroundService
{
    private readonly FetchOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Background job ad fetch is disabled");
            return;
        }

        try
        {
            await coordinator.WaitForNextRunAsync(TimeSpan.FromSeconds(_options.StartupDelaySeconds), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken);
                await coordinator.WaitForNextRunAsync(TimeSpan.FromMinutes(_options.IntervalMinutes), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        coordinator.MarkStarted();

        try
        {
            // The worker is a singleton; the ingestion service and its HttpClient are scoped.
            await using var scope = scopeFactory.CreateAsyncScope();
            var ingestion = scope.ServiceProvider.GetRequiredService<JobAdIngestionService>();
            var summary = await ingestion.RunAsync(cancellationToken);
            coordinator.MarkCompleted(summary, timeProvider.GetUtcNow());
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Anything unexpected (database down, bug) must not kill the worker; try again next interval.
            logger.LogError(ex, "Job ad fetch failed");
            coordinator.MarkFailed(timeProvider.GetUtcNow());
        }
    }
}

public static class FetchingSetup
{
    public static IServiceCollection AddJobAdFetching(this IServiceCollection services)
    {
        services.AddOptions<FetchOptions>()
            .BindConfiguration(FetchOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<FetchCoordinator>();
        services.AddHostedService<JobAdFetchWorker>();
        return services;
    }
}
