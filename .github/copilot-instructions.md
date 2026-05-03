# System Directives: .NET Conversational AI Architecture

## 1. Architectural Integrity & Boundaries
* **Core (Domain):** Pure business logic, entities, service interfaces, and custom exceptions. **Constraint:** Zero framework or SDK dependencies; restrict strictly to standard language libraries.
* **Infrastructure (Adapter):** Implements Core interfaces. **Constraint:** Owns external SDK integrations (e.g., `AWSSDK.BedrockRuntime`, `Amazon.*`).
* **API (Entry Point):** ASP.NET MVC Controllers. Translates HTTP -> Domain -> HTTP. **Constraint:** Owns `Microsoft.AspNetCore`. Enforce DTOs (Request/Response) at the Controller boundary to isolate API contracts. Map domain results directly to HTTP Status Codes.

## 2. Operations & Observability
* **Exceptions:** Emit RFC 7807 `ProblemDetails`. Map Domain Exceptions to specific 4xx codes. Unhandled exceptions are 500s; strictly mask stack traces and raw upstream errors (e.g., AWS details) from clients.
* **Logging:** Use structured logging (`ILogger`) tagged with `CorrelationId`. Record the intent (entry point) and outcome (exit point/duration).
* **Upstream HTTP:** Log outbound calls at `Debug` with full request body (endpoint, model, parameters, payload) *before* execution. Log `Warning` with status code, model, endpoint, and response body on non-2xx results.

## 3. AI & Bedrock Specifics
* **Statelessness:** Maintain a completely stateless Bedrock adapter. Isolate all conversation history management inside a dedicated Core service.
* **Resiliency:** Actively recover from `ThrottlingException` (HTTP 429) with exponential backoff or propagate "Service Busy". Validate prompt sizes against target model token limits.
* **Local LLMs:** Set `client.Timeout = TimeSpan.FromSeconds(120)` to override the default 30s `HttpClient` limit for local CPU-bound models.
* **Prompty Parsing:** Use `[ \t]*` (instead of `\s*`) after the colon in regexes to prevent matching across line boundaries in multiline YAML blocks.

## 4. Testing Strategy (xUnit)
* **Setup:** Use the class constructor for `beforeEach` shared setup. Express exactly one unique behavior per test.
* **Factories:** Extract private `CreateService(...)` helpers to supply default dependencies (e.g., `NullLogger<T>.Instance`), keeping test bodies focused on their unique intent.
* **Isolation:** Mock *only* external I/O boundaries. Allow Controller -> Service -> Adapter flows to execute using real implementations. Use `WebApplicationFactory` for integration tests.
* **State Machines:** Use `[Theory, InlineData]` to cover every invalid status transition in a single test method (one `InlineData` per disallowed status).
* **Verification:** After refactoring, write a numbered functional checklist (`dotnet test`, `dotnet run` + `curl`/`Scalar` steps) so developers can confirm zero regressions.

## 5. Development Standards
* **Tooling:** Target **.NET 10**. Add `<UserSecretsId>dotnet-ai-assistant-api</UserSecretsId>` in `Api.csproj` for `--id`-less usage.
* **Quality Gates:** Run `dotnet csharpier format .` and `semgrep scan --config auto --config semgrep-rules.yml .` before commits. Treat Semgrep rules as mandatory local fixes.
* **Plan Tracking:** Pair every `docs/plans/PLAN-NNN-*.md` with a live `PLAN-NNN-progress.md`. Mark items complete as they land, track out-of-plan fixes in a separate table, and update before ending the session.
* **Instruction Updates:** Append new conventions, constraints, or hard-won fixes to this file when it will provide the highest signal and clarity to future developers.
* **Razor Pages:** On forms calling slow upstreams, disable the submit button and show a CSS spinner via `addEventListener('submit', ...)`. Escape Razor syntax using `@@keyframes`.
* **WAF + EF Core provider conflict:** When replacing `AddDbContext` in `WebApplicationFactory.ConfigureServices`, use the **same** provider (e.g., SQLite with a temp-file path `DataSource=/tmp/test-{Guid}.db`) rather than swapping to the InMemory provider. EF Core registers provider-specific singleton services in the ASP.NET DI container; adding a second provider alongside causes an "only a single database provider" exception at startup. Temp-file SQLite is isolated per test class and works with `Database.Migrate()`.