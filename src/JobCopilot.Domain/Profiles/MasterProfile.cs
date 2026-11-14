namespace JobCopilot.Domain.Profiles;

/// <summary>
/// Everything the user can truthfully say about themselves. This is the single source of truth:
/// matching reads skills from it and the application prompt may only select and rephrase from it.
/// </summary>
public sealed class MasterProfile
{
    /// <summary>The application is single-user, so there is exactly one profile row.</summary>
    public const int SingletonId = 1;

    public int Id { get; private set; } = SingletonId;

    public string FullName { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Location { get; set; } = "";
    public string LinkedInUrl { get; set; } = "";
    public string GitHubUrl { get; set; } = "";
    public string WebsiteUrl { get; set; } = "";
    public string Summary { get; set; } = "";

    public List<string> Strengths { get; set; } = [];
    public List<Experience> Experiences { get; set; } = [];
    public List<ProfileProject> Projects { get; set; } = [];
    public List<Skill> Skills { get; set; } = [];
    public List<Education> Education { get; set; } = [];
    public List<SpokenLanguage> Languages { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// True when there is too little content to generate an application from.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(FullName)
        && Experiences.Count == 0
        && Projects.Count == 0
        && Skills.Count == 0
        && Education.Count == 0;

    /// <summary>
    /// Every skill the profile can back up: the explicit skill list plus technologies named on
    /// experiences and projects.
    /// </summary>
    public IEnumerable<string> AllSkillNames() =>
        Skills.Select(s => s.Name)
            .Concat(Experiences.SelectMany(e => e.Technologies))
            .Concat(Projects.SelectMany(p => p.Technologies))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim());

    /// <summary>
    /// Trims text and drops blank list entries so that half-filled form rows are never persisted.
    /// </summary>
    public void Normalize()
    {
        FullName = FullName.Trim();
        Headline = Headline.Trim();
        Email = Email.Trim();
        Phone = Phone.Trim();
        Location = Location.Trim();
        LinkedInUrl = LinkedInUrl.Trim();
        GitHubUrl = GitHubUrl.Trim();
        WebsiteUrl = WebsiteUrl.Trim();
        Summary = Summary.Trim();

        Strengths = CleanList(Strengths);
        Experiences = Experiences.Where(e => !e.IsBlank).ToList();
        Projects = Projects.Where(p => !p.IsBlank).ToList();
        Skills = Skills.Where(s => !string.IsNullOrWhiteSpace(s.Name)).ToList();
        Education = Education.Where(e => !e.IsBlank).ToList();
        Languages = Languages.Where(l => !string.IsNullOrWhiteSpace(l.Name)).ToList();

        foreach (var experience in Experiences)
        {
            experience.Technologies = CleanList(experience.Technologies);
        }

        foreach (var project in Projects)
        {
            project.Technologies = CleanList(project.Technologies);
        }

        foreach (var skill in Skills)
        {
            skill.Name = skill.Name.Trim();
            skill.Category = skill.Category.Trim();
        }
    }

    private static List<string> CleanList(IEnumerable<string> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
