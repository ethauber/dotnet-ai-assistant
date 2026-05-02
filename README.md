# dotnet-ai-assistant

Minimal .NET 10 conversational AI playground with a health endpoint and a prompt-driven repo assistant endpoint.
Conversational AI starter built with .NET 10 using a Clean Architecture split
across API, Core, and Infrastructure projects.

## Current Status

- API project is running with controller-based endpoints.
- Health endpoint is implemented and integration tested.
- Semantic Kernel chat endpoint is implemented and unit tested.
- Prompt-driven repo assistant endpoint is implemented and tested.
- OpenAPI is enabled in development and exposed via Scalar (opens automatically on `dotnet run`).
- Structured logging via Serilog with correlation ID middleware on every request.
- Step-by-step implementation plan docs are now in `docs/plans/`.

## Implemented Endpoints

- `GET /status`
	- Returns health status payload: `{ "status": "ok" }` or
		`{ "status": "degraded" }`.
- `POST /chat`
	- Accepts `{ "message": "..." }`.
	- Returns `{ "reply": "..." }` from Semantic Kernel chat completion.
- `POST /repo-assistant/run`
	- Accepts `{ "userGoal": "...", "fileContext": "...", "projectArea": "..." }`.
	- Returns `{ "reply": "..." }` from the prompt-driven repo assistant.

## Quick Start

1. Restore and build:

```bash
dotnet restore
dotnet build
```

2. Configure secrets for the chat model:

**Option A — OpenAI:**

```bash
dotnet user-secrets set "SemanticKernel:ApiKey" "<your-openai-key>" --project src/Api
```

**Option B — Ollama (local, no API key required):**

```bash
ollama serve          # start Ollama if not already running
ollama pull gemma3:4b  # or any model you prefer

dotnet user-secrets set "SemanticKernel:ApiKey" "ollama" --project src/Api
dotnet user-secrets set "SemanticKernel:ModelId" "gemma3:4b" --project src/Api
dotnet user-secrets set "SemanticKernel:Endpoint" "http://localhost:11434/v1" --project src/Api
```

3. Run the API:

```bash
dotnet run --project src/Api/Api.csproj
```

The browser opens automatically at `http://localhost:5000/scalar/v1`.

4. Explore API docs:

- OpenAPI: `http://localhost:5000/openapi/v1.json`
- Scalar UI (development): `http://localhost:5000/scalar/v1`

5. Run static analysis before pushing:

```bash
semgrep scan --config auto --config semgrep-rules.yml .
```

## Example Requests

Health check:

```bash
curl -s http://localhost:5000/status | jq .
```

Chat completion:

```bash
curl -s -X POST http://localhost:5000/chat \
	-H "Content-Type: application/json" \
	-d '{"message":"Say hello in one sentence"}' | jq .
```

Repo assistant:

```bash
curl -s -X POST http://localhost:5000/repo-assistant/run \
	-H "Content-Type: application/json" \
	-d '{"userGoal":"Summarize the API layer","projectArea":"api"}' | jq .
```

## Architecture

The solution follows a Clean Architecture flow:

- `src/Core`
	- Domain contracts and service interfaces.
	- Includes `IHealthStatusService`, `IChatService`, and `IRepoAssistantService`.
- `src/Infrastructure`
	- Adapter implementations and external SDK integration.
	- Includes `HealthStatusService`, `SemanticKernelChatService`, and `RepoAssistantService`.
	- References AWS Bedrock runtime SDK and Microsoft Semantic Kernel.
- `src/Api`
	- ASP.NET controllers, DTOs, dependency injection, and OpenAPI.
	- Includes `StatusController`, `ChatController`, and `RepoAssistantController`.

## Configuration

`src/Api/appsettings.json` includes:

```json
"SemanticKernel": {
	"ModelId": "gpt-4o-mini",
	"ApiKey": "",
	"Endpoint": ""
}
```

`SemanticKernel:Endpoint` is optional. Omit it for OpenAI. Set it to `http://localhost:11434/v1` for Ollama or any other OpenAI-compatible local endpoint.

Keep `SemanticKernel:ApiKey` in User Secrets for local development and out of
source control.

Prompt assets live in `prompts/`. The initial `repo-assistant.prompty` template is resolved from the repository root and used by the Infrastructure repo assistant service.

See `docs/repo-assistant.md` for the repo assistant request/response contract, runtime assumptions, and current limitations.

## Testing

Run all tests:

```bash
dotnet test
```

Current automated coverage includes:

- Integration tests for `/status` behavior (`ok` and `degraded`).
- Controller unit test for `/chat` response mapping via mocked `IChatService`.
- Endpoint and service tests for `/repo-assistant/run`.

## Code Quality

This project uses [Semgrep](https://semgrep.dev/) for static analysis. Run a scan locally before pushing:

```bash
semgrep scan --config auto --config semgrep-rules.yml .
```

Custom rules live in `semgrep-rules.yml`. Treat these scans as a local pre-push requirement for now.

## Plans And Iteration

- Active implementation plan:
	`docs/plans/PLAN-001-semantic-kernel-hello-world.md`

## References

- [Semantic Kernel Docs](https://learn.microsoft.com/en-us/semantic-kernel/overview/)
- [.NET AI Samples](https://github.com/dotnet/ai-samples)
- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp)
