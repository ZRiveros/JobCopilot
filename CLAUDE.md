# JobCopilot – conventions

Self-hosted job-search assistant and portfolio project. Single user, .NET 10, Blazor Web App
(Interactive Server), EF Core + PostgreSQL.

## Language

- Code, identifiers, comments, commit messages and docs for developers: **English**.
- Everything the user sees in the UI, the generated prompt and the PDFs: **Swedish**.
- README is written in Swedish (the audience is Swedish employers).

## Commands

```bash
dotnet build                      # warnings are errors
dotnet test                       # all tests; DB tests skip when no database is reachable
dotnet run --project src/JobCopilot.Web

# New migration (never edit an applied migration)
dotnet ef migrations add <Name> --project src/JobCopilot.Infrastructure --startup-project src/JobCopilot.Infrastructure
```

Database tests use Testcontainers. Without Docker, point them at an existing server with
`JOBCOPILOT_TEST_POSTGRES="Host=...;Username=...;Password=..."` (each test class creates and drops
its own database). CI sets `JOBCOPILOT_REQUIRE_DB_TESTS=true` so that they can never silently skip.

## Architecture

Dependencies point inwards: `Web → Infrastructure → Application → Domain`.

| Project | Contains | May reference |
|---|---|---|
| `Domain` | Entities, enums, pure rules (matching, kanban transitions). No I/O. | nothing |
| `Application` | Use-case services, ports (`IAppDbContext`, `IJobAdSource`, `IPdfRenderer`), prompt builder, response parser, Markdown subset. | Domain, EF Core abstractions |
| `Infrastructure` | `AppDbContext` + migrations, JobTech HTTP client, QuestPDF renderer. | Application |
| `Web` | Blazor components, minimal API endpoints, cookie auth, `JobAdFetchWorker`. | Application, Infrastructure (composition root only) |

Rules that keep this honest:

- **Blazor Server and DbContext**: never inject a `DbContext` into a component or a long-lived
  service. Services take `IAppDbContextFactory` and create one short-lived context per operation.
- Components call Application services directly. The minimal API exists for file downloads, auth
  and machine access – components do not call the app's own HTTP API.
- Put logic in pure classes (`JobMatcher`, `ApplicationPromptBuilder`, `ApplicationResponseParser`,
  `MarkdownParser`) and keep services as thin orchestration. Pure logic gets unit tests; anything
  touching EF gets an integration test against real PostgreSQL – no in-memory provider.
- Interfaces only where there is a real seam (I/O ports). No interface-per-service.
- Time comes from `TimeProvider`; never `DateTime.Now`/`UtcNow`.
- All timestamps are stored as UTC `DateTimeOffset`; convert to Swedish time at the edge (`SwedishTime`).

## Product invariants

- **No AI integration.** The app only builds a prompt and parses a pasted answer. Do not add an
  LLM SDK or outbound calls other than the JobTech API.
- **The master profile is the only source of truth** for generated material. The prompt's hard
  rules (select and rephrase only, never invent, answer in Swedish) and the response markers
  (`===CV===`, `===BREV===`, `===SLUT===`) are covered by tests – change them together.
- Contact details in PDFs come from the profile, never from the pasted answer.
- Ad text is untrusted input: it is rendered as plain text and fenced as data in the prompt.

## Code style

- File-scoped namespaces, primary constructors for DI, `sealed` by default, collection expressions.
- `async` methods end with `Async` and take a `CancellationToken` (last parameter).
- Entities guard their own invariants through methods (`JobApplication.MoveTo`), not public setters,
  except the profile sections, which are plain editable data.
- Tests: xUnit v3, names `Method_Scenario_Expectation`, one behaviour per test.
- UI: plain CSS in `wwwroot/app.css` (design tokens as CSS variables), no CSS framework, no JS
  except the small helpers in `wwwroot/app.js`.

## Git

One commit per logical unit, imperative subject line (`Add job matching`), body explains why when
it is not obvious.
