using JobCopilot.Application.Abstractions;
using JobCopilot.Infrastructure.JobTech;
using JobCopilot.Infrastructure.Pdf;
using JobCopilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobCopilot.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The database connection string is missing. Set ConnectionStrings:{ConnectionStringName} " +
                $"(environment variable ConnectionStrings__{ConnectionStringName}).");
        }

        services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<IAppDbContextFactory, AppDbContextFactory>();
        services.AddScoped<DatabaseInitializer>();

        services.AddDataProtection()
            .SetApplicationName("JobCopilot")
            .PersistKeysToDbContext<AppDbContext>();

        services.AddOptions<JobTechOptions>()
            .BindConfiguration(JobTechOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IJobAdSource, JobTechClient>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<JobTechOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            })
            // Retry with backoff on transient errors, per-attempt and total timeouts, circuit breaker.
            .AddStandardResilienceHandler();

        services.AddSingleton<IPdfRenderer, QuestPdfRenderer>();

        return services;
    }
}
