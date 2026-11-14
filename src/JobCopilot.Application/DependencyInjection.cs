using JobCopilot.Application.Applications;
using JobCopilot.Application.Documents;
using JobCopilot.Application.JobAds;
using JobCopilot.Application.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobCopilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // The services hold no state and create their own short-lived DbContext per call.
        // They are scoped (not singleton) because the job source behind ingestion is a typed HttpClient.
        services.AddScoped<ProfileService>();
        services.AddScoped<MatchingService>();
        services.AddScoped<SavedSearchService>();
        services.AddScoped<JobAdService>();
        services.AddScoped<JobAdIngestionService>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<DocumentExportService>();

        return services;
    }
}
