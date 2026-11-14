using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace JobCopilot.Web.Auth;

public static class AuthSetup
{
    public const string LoginRateLimitPolicy = "login";
    public const string LoginPath = "/login";

    /// <summary>
    /// Cookie authentication for the single user. Everything requires a login unless an endpoint
    /// explicitly opts out, so a new page or endpoint is protected by default.
    /// </summary>
    public static IServiceCollection AddSingleUserAuth(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>()
            .BindConfiguration(AuthOptions.SectionName)
            .Validate(
                options => options.HasUsablePassword,
                $"A login password of at least {AuthOptions.MinPasswordLength} characters is required. " +
                "Set JOBCOPILOT_PASSWORD in .env (docker compose) or Auth:Password via user-secrets / " +
                "the Auth__Password environment variable.")
            .Validate(options => options.SessionDays is >= 1 and <= 365, "Auth:SessionDays must be between 1 and 365.")
            .ValidateOnStart();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();

        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((cookie, auth) =>
            {
                cookie.Cookie.Name = "jobcopilot.auth";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;

                // Works both on plain HTTP in a home network and behind a TLS-terminating proxy.
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                cookie.LoginPath = LoginPath;
                cookie.ExpireTimeSpan = TimeSpan.FromDays(auth.Value.SessionDays);
                cookie.SlidingExpiration = true;

                // API callers get a status code instead of an HTML login page.
                cookie.Events.OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }
                    else
                    {
                        context.Response.Redirect(context.RedirectUri);
                    }

                    return Task.CompletedTask;
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddCascadingAuthenticationState();

        // Slows down password guessing: a handful of attempts per minute and client address.
        services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy(LoginRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(1) }));

            limiter.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Redirect($"{LoginPath}?error={LoginError.TooManyAttempts}");
                return ValueTask.CompletedTask;
            };
        });

        return services;
    }
}

/// <summary>Values of the <c>error</c> query parameter on the login page.</summary>
public static class LoginError
{
    public const string WrongPassword = "password";
    public const string TooManyAttempts = "throttled";
}
