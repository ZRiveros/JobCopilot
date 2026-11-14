using JobCopilot.Domain.Matching;

namespace JobCopilot.Domain.Tests.Matching;

public class SkillLexiconTests
{
    private static readonly SkillLexicon Lexicon = SkillLexicon.Create(SkillCatalog.Default);

    [Theory]
    [InlineData("csharp", "C#")]
    [InlineData("  c#  ", "C#")]
    [InlineData("dotnet", ".NET")]
    [InlineData("EF Core", "Entity Framework")]
    [InlineData("postgres", "PostgreSQL")]
    [InlineData("rest", "REST")]
    public void Canonicalize_KnownAlias_ReturnsCanonicalName(string input, string expected)
    {
        Assert.Equal(expected, Lexicon.Canonicalize(input));
    }

    [Fact]
    public void Canonicalize_UnknownSkill_ReturnsTrimmedInput()
    {
        Assert.Equal("Fortran", Lexicon.Canonicalize(" Fortran "));
    }

    [Fact]
    public void Detect_FindsSkillsByNameAndAlias()
    {
        var detected = Lexicon.Detect("Du har jobbat med C#, dotnet och EF Core mot Postgres.");

        Assert.Equivalent(new[] { "C#", ".NET", "Entity Framework", "PostgreSQL" }, detected, strict: true);
    }

    [Fact]
    public void Detect_LongerSkillShadowsShorterOneInsideIt()
    {
        var detected = Lexicon.Detect("Vi kör Azure DevOps och SQL Server.");

        Assert.Contains("Azure DevOps", detected);
        Assert.Contains("SQL Server", detected);
        Assert.DoesNotContain("Azure", detected);
        Assert.DoesNotContain("SQL", detected);
    }

    [Fact]
    public void Detect_ShorterSkillStillCountsWhenMentionedOnItsOwn()
    {
        var detected = Lexicon.Detect("Azure DevOps för CI/CD, driftat i Azure.");

        Assert.Contains("Azure DevOps", detected);
        Assert.Contains("Azure", detected);
    }

    [Fact]
    public void Detect_CaseSensitiveTerm_DoesNotMatchOrdinaryWord()
    {
        Assert.DoesNotContain("REST", Lexicon.Detect("You will own the rest of the pipeline."));
        Assert.Contains("REST", Lexicon.Detect("Du bygger REST-API:er."));
    }

    [Fact]
    public void Detect_EmptyText_ReturnsEmptySet()
    {
        Assert.Empty(Lexicon.Detect(null));
        Assert.Empty(Lexicon.Detect("   "));
    }

    [Fact]
    public void Create_ExtraSkill_IsDetectedEvenThoughCatalogLacksIt()
    {
        var lexicon = SkillLexicon.Create(SkillCatalog.Default, ["Fortran"]);

        Assert.Contains("Fortran", lexicon.Detect("Underhåll av äldre fortran-kod."));
    }

    [Fact]
    public void Create_ExtraSkillThatIsAnAlias_DoesNotCreateSecondSkill()
    {
        var lexicon = SkillLexicon.Create(SkillCatalog.Default, ["csharp"]);

        Assert.Equal("C#", Assert.Single(lexicon.Detect("csharp")));
    }

    [Fact]
    public void DefaultCatalog_HasNoDuplicateNamesOrAliases()
    {
        var terms = SkillCatalog.Default
            .SelectMany(t => t.SearchTerms.Select(term => (Term: term.ToUpperInvariant(), t.Name)))
            .Distinct()
            .GroupBy(pair => pair.Term)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(terms);
    }
}
