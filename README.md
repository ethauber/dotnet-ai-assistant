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

## Code Quality

This project uses [Semgrep](https://semgrep.dev/) for static analysis. Run a scan locally before pushing:

```bash
semgrep scan --config auto --config semgrep-rules.yml .
```

Custom rules live in `semgrep-rules.yml`. The CI/CD pipeline enforces these scans on all pull requests.