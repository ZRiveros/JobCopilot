namespace JobCopilot.Domain.Matching;

/// <summary>
/// The built-in vocabulary of skills that are looked for in job ads. It is deliberately focused on
/// what shows up in Swedish .NET / web developer ads; skills from the master profile that are not
/// listed here are added to the lexicon at runtime (see <see cref="SkillLexicon"/>).
/// </summary>
public static class SkillCatalog
{
    public static IReadOnlyList<SkillTerm> Default { get; } =
    [
        // Languages
        new("C#", SkillCategory.Language) { Aliases = ["csharp", "c-sharp", "c sharp"] },
        new("F#", SkillCategory.Language) { Aliases = ["fsharp"] },
        new("VB.NET", SkillCategory.Language) { Aliases = ["Visual Basic"] },
        new("Java", SkillCategory.Language),
        new("Kotlin", SkillCategory.Language),
        new("Python", SkillCategory.Language),
        new("JavaScript", SkillCategory.Language) { Aliases = ["ECMAScript"] },
        new("TypeScript", SkillCategory.Language),
        new("C++", SkillCategory.Language) { Aliases = ["cpp"] },
        new("Golang", SkillCategory.Language),
        new("Rust", SkillCategory.Language) { MatchCase = true },
        new("PHP", SkillCategory.Language),
        new("Swift", SkillCategory.Language) { MatchCase = true },
        new("SQL", SkillCategory.Language),
        new("T-SQL", SkillCategory.Language) { Aliases = ["TSQL", "Transact-SQL"] },
        new("PowerShell", SkillCategory.Language),
        new("Bash", SkillCategory.Language),
        new("HTML", SkillCategory.Language) { Aliases = ["HTML5"] },
        new("CSS", SkillCategory.Language) { Aliases = ["CSS3"] },

        // .NET ecosystem
        new(".NET", SkillCategory.Framework) { Aliases = ["dotnet", "dot net", ".NET Core", ".NET Framework"] },
        new("ASP.NET Core", SkillCategory.Framework) { Aliases = ["ASP.NET", "ASPNET"] },
        new("Blazor", SkillCategory.Framework),
        new("Entity Framework", SkillCategory.Framework) { Aliases = ["EF Core", "Entity Framework Core", "EntityFramework"] },
        new("Dapper", SkillCategory.Framework),
        new("LINQ", SkillCategory.Framework),
        new("Minimal API", SkillCategory.Framework) { Aliases = ["Minimal APIs"] },
        new("Web API", SkillCategory.Framework) { Aliases = ["WebAPI"] },
        new("MVC", SkillCategory.Framework),
        new("Razor", SkillCategory.Framework),
        new("SignalR", SkillCategory.Framework),
        new("WPF", SkillCategory.Framework),
        new("WinForms", SkillCategory.Framework) { Aliases = ["Windows Forms"] },
        new("MAUI", SkillCategory.Framework) { Aliases = [".NET MAUI"] },
        new("Xamarin", SkillCategory.Framework),
        new("Unity", SkillCategory.Framework) { MatchCase = true },
        new("MediatR", SkillCategory.Framework),
        new("NuGet", SkillCategory.Tool),
        new("Umbraco", SkillCategory.Framework),
        new("Optimizely", SkillCategory.Framework) { Aliases = ["Episerver", "EPiServer"] },
        new("Sitecore", SkillCategory.Framework),
        new("Spring Boot", SkillCategory.Framework) { Aliases = ["Spring"], MatchCase = true },
        new("Node.js", SkillCategory.Framework) { Aliases = ["NodeJS", "Node"], MatchCase = true },

        // Frontend
        new("React", SkillCategory.Frontend) { Aliases = ["React.js", "ReactJS"] },
        new("Angular", SkillCategory.Frontend) { Aliases = ["AngularJS"] },
        new("Vue", SkillCategory.Frontend) { Aliases = ["Vue.js", "VueJS"], MatchCase = true },
        new("Svelte", SkillCategory.Frontend),
        new("Next.js", SkillCategory.Frontend) { Aliases = ["NextJS"] },
        new("Tailwind", SkillCategory.Frontend) { Aliases = ["Tailwind CSS", "TailwindCSS"] },
        new("Bootstrap", SkillCategory.Frontend),
        new("SASS", SkillCategory.Frontend) { Aliases = ["SCSS"] },
        new("WebAssembly", SkillCategory.Frontend) { Aliases = ["WASM"] },

        // Data
        new("SQL Server", SkillCategory.Data) { Aliases = ["MSSQL", "MS SQL", "Microsoft SQL Server", "SQLServer"] },
        new("PostgreSQL", SkillCategory.Data) { Aliases = ["Postgres"] },
        new("MySQL", SkillCategory.Data) { Aliases = ["MariaDB"] },
        new("SQLite", SkillCategory.Data),
        new("Oracle", SkillCategory.Data) { MatchCase = true },
        new("MongoDB", SkillCategory.Data) { Aliases = ["Mongo"] },
        new("Redis", SkillCategory.Data),
        new("Cosmos DB", SkillCategory.Data) { Aliases = ["CosmosDB"] },
        new("Elasticsearch", SkillCategory.Data) { Aliases = ["Elastic Search", "OpenSearch"] },
        new("NoSQL", SkillCategory.Data),
        new("Power BI", SkillCategory.Data) { Aliases = ["PowerBI"] },
        new("ETL", SkillCategory.Data),

        // Cloud
        new("Azure", SkillCategory.Cloud) { Aliases = ["Microsoft Azure"] },
        new("AWS", SkillCategory.Cloud) { Aliases = ["Amazon Web Services"] },
        new("GCP", SkillCategory.Cloud) { Aliases = ["Google Cloud"] },
        new("Azure Functions", SkillCategory.Cloud),
        new("Azure Service Bus", SkillCategory.Cloud) { Aliases = ["Service Bus"] },
        new("App Service", SkillCategory.Cloud) { Aliases = ["Azure App Service"] },
        new("Entra ID", SkillCategory.Cloud) { Aliases = ["Azure AD", "Azure Active Directory", "Active Directory"] },
        new("Serverless", SkillCategory.Cloud),

        // DevOps
        new("Docker", SkillCategory.DevOps),
        new("Kubernetes", SkillCategory.DevOps) { Aliases = ["K8s", "AKS"] },
        new("CI/CD", SkillCategory.DevOps) { Aliases = ["CICD", "CI / CD", "Continuous Integration", "Continuous Delivery", "Continuous Deployment"] },
        new("Azure DevOps", SkillCategory.DevOps),
        new("GitHub Actions", SkillCategory.DevOps),
        new("GitLab", SkillCategory.DevOps) { Aliases = ["GitLab CI"] },
        new("Jenkins", SkillCategory.DevOps),
        new("TeamCity", SkillCategory.DevOps),
        new("Octopus Deploy", SkillCategory.DevOps),
        new("Terraform", SkillCategory.DevOps),
        new("Bicep", SkillCategory.DevOps) { MatchCase = true },
        new("Ansible", SkillCategory.DevOps),
        new("Linux", SkillCategory.DevOps),
        new("DevOps", SkillCategory.DevOps),
        new("Infrastructure as Code", SkillCategory.DevOps) { Aliases = ["IaC"] },

        // Architecture and integration
        new("REST", SkillCategory.Architecture) { Aliases = ["RESTful", "Restful", "REST-API", "REST API", "REST-api", "Rest API", "Rest-API"], MatchCase = true },
        new("GraphQL", SkillCategory.Architecture),
        new("gRPC", SkillCategory.Architecture),
        new("Microservices", SkillCategory.Architecture) { Aliases = ["Mikrotjänster", "Microservice", "Mikrotjänst"] },
        new("Event-driven", SkillCategory.Architecture) { Aliases = ["Event driven", "Händelsedriven", "Händelsestyrd", "Eventdriven"] },
        new("RabbitMQ", SkillCategory.Architecture),
        new("Kafka", SkillCategory.Architecture),
        new("DDD", SkillCategory.Architecture) { Aliases = ["Domain-Driven Design", "Domain Driven Design"] },
        new("Clean Architecture", SkillCategory.Architecture),
        new("CQRS", SkillCategory.Architecture),
        new("SOLID", SkillCategory.Architecture) { MatchCase = true },
        new("Design Patterns", SkillCategory.Architecture) { Aliases = ["Designmönster"] },
        new("OAuth", SkillCategory.Architecture) { Aliases = ["OAuth2", "OAuth 2.0", "OpenID Connect", "OIDC"] },

        // Testing
        new("xUnit", SkillCategory.Testing),
        new("NUnit", SkillCategory.Testing),
        new("MSTest", SkillCategory.Testing),
        new("TDD", SkillCategory.Testing) { Aliases = ["Test-Driven Development", "Testdriven utveckling", "Testdriven"] },
        new("Unit Testing", SkillCategory.Testing) { Aliases = ["Unit tests", "Enhetstester", "Enhetstest", "Enhetstestning"] },
        new("Integration Testing", SkillCategory.Testing) { Aliases = ["Integration tests", "Integrationstester", "Integrationstest"] },
        new("Test Automation", SkillCategory.Testing) { Aliases = ["Testautomatisering", "Automated testing", "Automatiserade tester"] },
        new("Playwright", SkillCategory.Testing),
        new("Selenium", SkillCategory.Testing),
        new("Cypress", SkillCategory.Testing),

        // Ways of working
        new("Agile", SkillCategory.Practice) { Aliases = ["Agil", "Agilt", "Agila"] },
        new("Scrum", SkillCategory.Practice),
        new("Kanban", SkillCategory.Practice),
        new("SAFe", SkillCategory.Practice) { MatchCase = true },
        new("Code Review", SkillCategory.Practice) { Aliases = ["Kodgranskning", "Code reviews", "Kodgranskningar"] },
        new("Pair Programming", SkillCategory.Practice) { Aliases = ["Parprogrammering", "Mob programming", "Mobbprogrammering"] },

        // Tools
        new("Git", SkillCategory.Tool),
        new("GitHub", SkillCategory.Tool),
        new("Bitbucket", SkillCategory.Tool),
        new("Jira", SkillCategory.Tool),
        new("Confluence", SkillCategory.Tool),
        new("Visual Studio", SkillCategory.Tool),
        new("VS Code", SkillCategory.Tool) { Aliases = ["Visual Studio Code", "VSCode"] },
        new("Rider", SkillCategory.Tool) { MatchCase = true },
        new("Swagger", SkillCategory.Tool) { Aliases = ["OpenAPI"] },
        new("Postman", SkillCategory.Tool),
        new("SonarQube", SkillCategory.Tool),
        new("Application Insights", SkillCategory.Tool),
        new("Grafana", SkillCategory.Tool),
        new("OpenTelemetry", SkillCategory.Tool),

        // Other
        new("AI", SkillCategory.Other) { Aliases = ["LLM", "Generative AI", "Generativ AI"], MatchCase = true },
        new("Machine Learning", SkillCategory.Other) { Aliases = ["Maskininlärning", "ML.NET"] },
        new("Embedded", SkillCategory.Other) { Aliases = ["embedded systems", "Inbyggda system", "inbyggda system"], MatchCase = true },
        new("SAP", SkillCategory.Other) { MatchCase = true },
        new("Dynamics 365", SkillCategory.Other) { Aliases = ["D365", "Dynamics"], MatchCase = true },
        new("SharePoint", SkillCategory.Other),
        new("Power Platform", SkillCategory.Other) { Aliases = ["Power Apps", "PowerApps", "Power Automate"] },
    ];
}
