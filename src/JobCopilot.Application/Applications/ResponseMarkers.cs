namespace JobCopilot.Application.Applications;

/// <summary>
/// The markers that structure the AI answer. The prompt asks for them and the parser splits on
/// them, so both sides use these constants.
/// </summary>
public static class ResponseMarkers
{
    public const string Cv = "===CV===";
    public const string CoverLetter = "===BREV===";
    public const string End = "===SLUT===";
}
