namespace JobCopilot.Domain.Profiles;

public sealed class Experience
{
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Location { get; set; } = "";
    public DateOnly? StartDate { get; set; }

    /// <summary>Null means the position is ongoing.</summary>
    public DateOnly? EndDate { get; set; }

    public string Description { get; set; } = "";
    public List<string> Technologies { get; set; } = [];

    public bool IsBlank => string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(Company);
}

public sealed class ProfileProject
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Url { get; set; } = "";
    public List<string> Technologies { get; set; } = [];

    public bool IsBlank => string.IsNullOrWhiteSpace(Name);
}

public sealed class Skill
{
    public string Name { get; set; } = "";

    /// <summary>Free-text grouping shown in the profile and the prompt, e.g. "Backend".</summary>
    public string Category { get; set; } = "";

    public SkillLevel Level { get; set; } = SkillLevel.Intermediate;
}

public enum SkillLevel
{
    Basic = 0,
    Intermediate = 1,
    Advanced = 2,
}

public sealed class Education
{
    public string School { get; set; } = "";
    public string Program { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Description { get; set; } = "";

    public bool IsBlank => string.IsNullOrWhiteSpace(School) && string.IsNullOrWhiteSpace(Program);
}

public sealed class SpokenLanguage
{
    public string Name { get; set; } = "";

    /// <summary>Free text, e.g. "Modersmål" or "Flytande".</summary>
    public string Level { get; set; } = "";
}
