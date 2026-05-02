# System Directives: .NET Conversational AI Architecture

## 1. Architectural Integrity & Data Flow
**Objective:** Maintain strict unidirectionality and separation of concerns via Clean Architecture principles.

### Core (The Domain)
* **Content:** Encapsulate pure business logic, Domain Entities, Service Interfaces, and Custom Exceptions.
* **Dependency Scope:** Restrict dependencies to standard language libraries and internal domain objects.
* **Package Placement:** Locate infrastructure-specific packages (e.g., `Microsoft.AspNetCore`, `Amazon.*`) exclusively outside this layer.

### Infrastructure (The Adapter)
* **Role:** Implement interfaces defined within the `Core` layer.
* **Dependency Rule:** Import `Core` and necessary external SDKs (e.g., `AWSSDK.BedrockRuntime`) to fulfill interface contracts.

### API (The Entry Point)
* **Role:** Orchestrate requests and translate protocols (HTTP -> Domain -> HTTP).
* **Pattern:** Implement **ASP.NET MVC Controllers**.
* **Delegation:** Delegate all business logic execution to Services or Mediators.
* **Response Mapping:** Map domain results directly to appropriate HTTP Status Codes.
* **Contract Isolation:** Enforce the use of DTOs (Request/Response models) at the Controller boundary to separate API contracts from internal Domain entities.

## 2. Operational Semantics
### Exception Handling
* **Standard:** Format all non-2xx responses using RFC 7807 **ProblemDetails**.
* **Mapping Strategy:** Capture known Domain Exceptions (e.g., `ModelNotFoundException`) and assign specific 4xx status codes. Treat unhandled exceptions as 500 Internal Server Errors.
* **Security:** Sanitize client-facing error messages. Mask stack traces and raw upstream errors (e.g., AWS SDK details) to prevent information leakage.

### Observability
* **Logging Standard:** Utilize Structured Logging (e.g., `ILogger`).
* **Content Strategy:** Record the **intent** (entry point) and **outcome** (exit point/duration) of operations.
* **Traceability:** Tag all log entries with a `CorrelationId` to ensure request continuity.

## 3. Domain Specifics: AI & AWS Bedrock
* **Domain Context:** Optimize for **Conversational AI** workflows.
* **Resiliency:**
    * **Throttling:** Implement active recovery for `ThrottlingException` (HTTP 429). Utilize exponential backoff strategies or propagate specific "Service Busy" signals.
    * **Token Management:** Validate prompt construction against the specific token limits of the target Bedrock model.
* **State Management:** Maintain a stateless architecture within the Bedrock adapter. Isolate conversation history management within a dedicated `Core` service.

## 4. Development Standards
* **Runtime:** Target **.NET 10**.
* **User Secrets:** Always include `<UserSecretsId>` in `Api.csproj` so `dotnet user-secrets` works without `--id`. Use a stable string like `dotnet-ai-assistant-api`.
* **Test Structure:** Use the xUnit class constructor as the `beforeEach` equivalent — initialise shared fixtures as private fields and extract repeated service-replacement logic into `private` factory helpers; each test should express only its unique intent.
* **Plan Tracking:** When creating a phased plan at `docs/plans/PLAN-NNN-*.md`, immediately create a companion `PLAN-NNN-progress.md` with the full task list in a pending state. Keep the progress doc current: mark items complete as they land, add any out-of-plan fixes in a separate table, and update it before ending a working session.
* **Instruction Updates:** When a session surfaces a new convention, constraint, or hard-won fix that would save future effort, add the minimal salient rule to this file before the session ends.
* **Code Formatting:** Use `dotnet csharpier format .` to enforce consistent C# style before committing.
* **Static Analysis:** Run `semgrep scan --config auto --config semgrep-rules.yml .` to detect code quality issues before committing or opening a PR. Rules are defined in `semgrep-rules.yml`; treat violations as issues to fix during local development.
* **Testing Strategy:**
    * **Unit Tests:** Validate `Core` logic in isolation using mocked interfaces. Keep tests minimal and focused — one behaviour per test, no redundant assertions, no setup that isn't exercised by the test.
    * **Integration Tests:** Utilize `WebApplicationFactory` to verify the full execution pipeline.
    * **Mocking Scope:** Target mocks specifically at external I/O boundaries (e.g., AWS Bedrock client), allowing the Controller -> Service -> Adapter flow to execute as real implementations.
    * **Refactoring:** After refactoring existing tests or production code, include a numbered functional verification checklist (e.g., `dotnet test`, `dotnet run` + curl/Scalar steps) so another developer can confirm nothing regressed.