using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JobCopilot.Web.Auth;

public static class AuthEndpoints
{
    public const string UserName = "owner";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Signing in has to happen in a normal HTTP request (a Blazor circuit cannot set cookies),
        // so the login page posts a plain form here. Form binding validates the antiforgery token.
        endpoints.MapPost("/account/login", async (
                [FromForm] string? password,
                [FromForm] string? returnUrl,
                HttpContext httpContext,
                IOptions<AuthOptions> options) =>
            {
                var target = ToLocalUrl(returnUrl);

                if (!PasswordMatches(password ?? "", options.Value.Password))
                {
                    var query = QueryString.Create(
                    [
                        KeyValuePair.Create<string, string?>("error", LoginError.WrongPassword),
                        KeyValuePair.Create<string, string?>("returnUrl", target),
                    ]);
                    return Results.LocalRedirect(AuthSetup.LoginPath + query);
                }

                var identity = new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, UserName)], CookieAuthenticationDefaults.AuthenticationScheme);

                await httpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity),
                    new AuthenticationProperties { IsPersistent = true });

                return Results.LocalRedirect(target);
            })
            .AllowAnonymous()
            .RequireRateLimiting(AuthSetup.LoginRateLimitPolicy)
            .ExcludeFromDescription();

        // No antiforgery token here: the auth cookie is SameSite=Lax, so a cross-site POST arrives
        // without it and is rejected by the authorization requirement before it can sign anyone out.
        endpoints.MapPost("/account/logout", async (HttpContext httpContext) =>
            {
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.LocalRedirect(AuthSetup.LoginPath);
            })
            .DisableAntiforgery()
            .ExcludeFromDescription();

        return endpoints;
    }

    /// <summary>
    /// Compares in constant time, and hashes first so the comparison does not leak the length.
    /// </summary>
    internal static bool PasswordMatches(string supplied, string expected)
    {
        if (expected.Length == 0)
        {
            return false;
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }

    /// <summary>
    /// Only allows redirects within the app, to prevent the login from being used as an open redirect.
    /// </summary>
    internal static string ToLocalUrl(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl)
            || returnUrl[0] != '/'
            || returnUrl.StartsWith("//", StringComparison.Ordinal)
            || returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            || returnUrl.Any(char.IsControl))
        {
            return "/";
        }

        return returnUrl;
    }
}
