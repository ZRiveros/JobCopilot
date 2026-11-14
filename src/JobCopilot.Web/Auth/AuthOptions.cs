namespace JobCopilot.Web.Auth;

/// <summary>
/// Single-user login. The password comes from configuration (<c>Auth__Password</c>, which
/// docker compose fills from <c>JOBCOPILOT_PASSWORD</c> in <c>.env</c>) and is never stored by the app.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public const int MinPasswordLength = 8;

    public string Password { get; set; } = "";

    /// <summary>How long a login lasts without activity.</summary>
    public int SessionDays { get; set; } = 14;

    public bool HasUsablePassword => !string.IsNullOrWhiteSpace(Password) && Password.Length >= MinPasswordLength;
}
