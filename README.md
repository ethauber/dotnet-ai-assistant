# dotnet-ai-assistant

Conversational AI starter built with .NET 10 using a Clean Architecture split
across API, Core, and Infrastructure projects.

## Current Status

- API project is running with controller-based endpoints.
- Health endpoint is implemented and integration tested.
- Semantic Kernel chat endpoint is implemented and unit tested.
- OpenAPI is enabled in development and exposed via Scalar.
- Step-by-step implementation plan docs are now in `docs/plans/`.

## Implemented Endpoints

- `GET /status`
	- Returns health status payload: `{ "status": "ok" }` or
		`{ "status": "degraded" }`.
- `POST /chat`
	- Accepts `{ "message": "..." }`.
	- Returns `{ "reply": "..." }` from Semantic Kernel chat completion.

## Quick Start

1. Restore and build:

```bash
dotnet restore
dotnet build
```

2. Configure local secret for Semantic Kernel API key:

```bash
dotnet user-secrets init --project src/Api
dotnet user-secrets set "SemanticKernel:ApiKey" "<your-api-key>" --project src/Api
```

3. Run the API:

```bash
dotnet run --project src/Api/Api.csproj
```

4. Explore API docs:

- OpenAPI: `http://localhost:5176/openapi/v1.json`
- Scalar UI (development): available when running locally in Development

## Example Requests

Health check:

```bash
curl -s http://localhost:5176/status | jq .
```

Chat completion:

```bash
curl -s -X POST http://localhost:5176/chat \
	-H "Content-Type: application/json" \
	-d '{"message":"Say hello in one sentence"}' | jq .
```

## Architecture

The solution follows a Clean Architecture flow:

- `src/Core`
	- Domain contracts and service interfaces.
	- Includes `IHealthStatusService` and `IChatService`.
- `src/Infrastructure`
	- Adapter implementations and external SDK integration.
	- Includes `HealthStatusService` and `SemanticKernelChatService`.
	- References AWS Bedrock runtime SDK and Microsoft Semantic Kernel.
- `src/Api`
	- ASP.NET controllers, DTOs, dependency injection, and OpenAPI.
	- Includes `StatusController` and `ChatController`.

## Configuration

`src/Api/appsettings.json` includes:

```json
"SemanticKernel": {
	"ModelId": "gpt-4o-mini",
	"ApiKey": ""
}
```

Keep `SemanticKernel:ApiKey` in User Secrets for local development and out of
source control.

## Testing

Run all tests:

```bash
dotnet test
```

Current automated coverage includes:

- Integration tests for `/status` behavior (`ok` and `degraded`).
- Controller unit test for `/chat` response mapping via mocked `IChatService`.

## Project Structure

```text
dotnet-ai-assistant/
├── src/
│   ├── Api/
│   ├── Core/
│   └── Infrastructure/
├── tests/
│   └── Tests/
├── docs/
│   └── plans/
└── dotnet-ai-assistant.sln
```

## Plans And Iteration

- Active implementation plan:
	`docs/plans/PLAN-001-semantic-kernel-hello-world.md`

## References

- [Semantic Kernel Docs](https://learn.microsoft.com/en-us/semantic-kernel/overview/)
- [.NET AI Samples](https://github.com/dotnet/ai-samples)
- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp)
