using System.Text.Json.Serialization;
using JobCopilot.Application;
using JobCopilot.Infrastructure;
using JobCopilot.Infrastructure.Persistence;
using JobCopilot.Web.Api;
using JobCopilot.Web.Auth;
using JobCopilot.Web.Components;
using JobCopilot.Web.Fetching;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleUserAuth();
builder.Services.AddJobAdFetching();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    // Pasted ads and AI answers travel over the circuit as one message; the 32 kB default is too small.
    .AddHubOptions(hub => hub.MaximumReceiveMessageSize = 512 * 1024);

builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

// Migrations run before the app starts taking requests, so a deploy is just "start the new image".
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// No HTTPS redirection or HSTS here: the app is meant to sit behind a reverse proxy that
// terminates TLS (set ASPNETCORE_FORWARDEDHEADERS_ENABLED=true so the app sees the original scheme).

// API clients should get bare status codes, pages a friendly "not found".
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    pages => pages.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// The login page needs its stylesheet before anyone is logged in.
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAuthEndpoints();
app.MapApiEndpoints();
app.MapHealthChecks("/healthz").AllowAnonymous();

app.Run();

public partial class Program;
