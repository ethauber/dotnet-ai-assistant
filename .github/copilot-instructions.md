# System Directives: .NET Conversational AI Architecture

## 1. Architectural Integrity & Boundaries
* **Core (Domain):** Pure business logic, entities, service interfaces, and custom exceptions. **Constraint:** Zero framework or SDK dependencies; restrict strictly to standard language libraries.
* **Infrastructure (Adapter):** Implements Core interfaces. **Constraint:** Owns external SDK integrations (e.g., `AWSSDK.BedrockRuntime`, `Amazon.*`).
* **API (Entry Point):** ASP.NET MVC Controllers. Translates HTTP -> Domain -> HTTP. **Constraint:** Owns `Microsoft.AspNetCore`. Enforce DTOs (Request/Response) at the Controller boundary to isolate API contracts. Map domain results directly to HTTP Status Codes.

## 2. Operations & Observability
* **Exceptions:** Emit RFC 7807 `ProblemDetails`. Map Domain Exceptions to specific 4xx codes. Unhandled exceptions are 500s; strictly mask stack traces and raw upstream errors (e.g., AWS details) from clients.
* **Logging:** Use structured logging (`ILogger`) tagged with `CorrelationId`. Record the intent (entry point) and outcome (exit point/duration).
* **Upstream HTTP:** Log outbound calls at `Debug` with full request body (endpoint, model, parameters, payload) *before* execution. Log `Warning` with status code, model, endpoint, and response body on non-2xx results.
* **DB Log Sink:** Register `RunLogSink` as a singleton `ILogEventSink` using `IServiceScopeFactory`. Filter `Information`+ logs from API/Infra/Core by `{RunId}`, ensuring all exceptions are swallowed.

## 3. AI & Bedrock Specifics
* **Statelessness:** Maintain a completely stateless Bedrock adapter. Isolate all conversation history management inside a dedicated Core service.
* **Resiliency:** Actively recover from `ThrottlingException` (HTTP 429) with exponential backoff or propagate "Service Busy". Validate prompt sizes against target model token limits.
* **Local LLMs:** Set `client.Timeout = TimeSpan.FromSeconds(120)` to override the default 30s `HttpClient` limit for local CPU-bound models.
* **Prompty Parsing:** Use `[ \t]*` (instead of `\s*`) after the colon in regexes to prevent matching across line boundaries in multiline YAML blocks. Keep the prompty system prompt perfectly synced with current architecture to avoid stale AI suggestions.

## 4. Testing Strategy (xUnit)
* **Setup:** Use the class constructor for `beforeEach` shared setup. Express exactly one unique behavior per test.
* **Factories:** Extract private `CreateService(...)` helpers to supply default dependencies (e.g., `NullLogger<T>.Instance`), keeping test bodies focused on their unique intent.
* **Isolation & WAF:** Mock *only* external I/O boundaries. Use `WebApplicationFactory` for integration tests, maintaining the exact same EF provider (e.g., temp-file SQLite `DataSource=/tmp/test-{Guid}.db`) instead of swapping to InMemory to prevent singleton provider DI crashes.
* **State Machines:** Use `[Theory, InlineData]` to cover every invalid status transition in a single test method (one `InlineData` per disallowed status).
* **Verification:** After refactoring, write a numbered functional checklist (`dotnet test`, `dotnet run` + `curl`/`Scalar` steps) so developers can confirm zero regressions.

## 5. Development Standards
* **Tooling:** Target **.NET 10** and use `http://localhost:50123` (avoiding macOS port 5000 conflicts). Add `<UserSecretsId>dotnet-ai-assistant-api</UserSecretsId>` in `Api.csproj` for `--id`-less usage.
* **Quality Gates:** Gate commits with `dotnet csharpier format .`, `semgrep scan --config auto --config semgrep-rules.yml .`, and the mandatory `scripts/check-migrations.sh` pre-commit hook.
* **EF Core Data:** Migrations are strictly mandatory for any `DbSet` change; never handwrite them. Suppress `PendingModelChangesWarning` exactly once inside `AssistantDbContext.OnConfiguring`.
* **Razor Pages UX:** On *every* form calling a slow upstream, disable the submit button independently via `addEventListener('submit', ...)` and show a context-specific CSS spinner. Escape Razor syntax in CSS using `@@keyframes`.
* **Workflow:** Pair every `docs/plans/PLAN-NNN-*.md` with a live `PLAN-NNN-progress.md`. Mark items complete as they land, track out-of-plan fixes, and append new hard-won conventions to this directives file immediately.