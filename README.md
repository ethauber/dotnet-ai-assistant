# dotnet-ai-assistant

Minimal .NET 10 conversational AI playground with a health endpoint and a prompt-driven repo assistant endpoint.

Run the API with:

```bash
dotnet run --project ./src/Api/Api.csproj
```

Then use:

- `GET /status` for the health check
- `POST /repo-assistant/run` for the prompt-driven repo assistant
- OpenAPI at `http://localhost:5176/openapi/v1.json`

Prompt assets live in `prompts/`. The initial `repo-assistant.prompty` template is resolved from the repository root and used by the Infrastructure repo assistant service.

See `docs/repo-assistant.md` for the request/response contract, runtime assumptions, and current limitations.