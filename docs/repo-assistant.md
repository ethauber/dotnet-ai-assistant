# Repo Assistant

The repo assistant is a prompt-driven implementation assistant that answers questions and suggests
targeted code changes based on a live snapshot of this codebase. It is NOT a general-purpose
chatbot — every request is grounded in the actual source tree.

## Architecture

```
POST /repo-assistant/run
  → RepoAssistantController
      → IRepoAssistantService (RepoAssistantService)
          ├── loads prompts/repo-assistant.prompty
          ├── builds dynamic context via Roslyn AST (src/ file tree + structural summaries + Core verbatim)
          └── calls OpenAI-compatible chat completions endpoint with exponential-backoff retry
```

## What is already implemented across the whole repo

### Persistence
- EF Core + SQLite via `AssistantDbContext` (`AssistantRun` + `AssistantRunEvent` tables)
- `IAssistantRunRepository` / `AssistantRunRepository`: `AddAsync`, `GetByIdAsync`, `UpdateAsync`, `ListRecentAsync`, `AddEventAsync`, `GetEventsForRunAsync`

### Workflow service
- `IAssistantRunService` / `AssistantRunService`: `CreateAsync`, `GenerateDraftAsync`, `ApproveAsync`, `RejectAsync`, `EditAndApproveAsync`, `RegenerateAsync`
- State machine: `Submitted → DraftGenerated → NeedsHumanReview → Approved / Rejected / Revised`
- Invalid transitions throw `InvalidOperationException` → HTTP 409

### API endpoints (`AssistantRunsController`)
| Method | Route | Description |
|--------|-------|-------------|
| POST | `/assistant-runs` | Create a new run |
| POST | `/assistant-runs/{id}/generate-draft` | Call AI to generate draft |
| POST | `/assistant-runs/{id}/approve` | Approve draft as-is |
| POST | `/assistant-runs/{id}/reject` | Reject draft |
| POST | `/assistant-runs/{id}/edit-and-approve` | Save edited output and approve |
| POST | `/assistant-runs/{id}/regenerate` | Reset and regenerate draft |
| GET | `/assistant-runs/{id}` | Get a single run |
| GET | `/assistant-runs` | List recent runs |

All endpoints return RFC 7807 `ProblemDetails` on 404 and 409.

### Other endpoints
- `GET /status` — health check via `HealthStatusService`
- `POST /chat` — single-turn chat via `SemanticKernelChatService` (Ollama / OpenAI-compatible)
- `POST /repo-assistant/run` — this endpoint

### Razor Pages UI (`/Pages/`)
| Page | Route | Description |
|------|-------|-------------|
| Index | `/` | Recent runs list |
| Submit | `/Submit` | Goal input form with AI spinner |
| Review | `/Review/{id}` | Draft review: approve / reject / edit-and-approve / regenerate; audit trail |
| Runs | `/Runs` | Full run list |
| Error | `/error/{statusCode}` | Custom error page for 404, 409, 500 |

All forms that trigger slow upstreams show a CSS spinner with context-specific messages.

### Observability
- Serilog structured logging (console + rolling file) with `CorrelationIdMiddleware`
- `ILogger<T>` in every service; `LogDebug` before HTTP calls, `LogWarning` on non-2xx

### Context generation (this endpoint)
On every request, `RepoAssistantService.GenerateRepoContextAsync` builds:
1. **Manifest** — explicit bullet list of everything already implemented (survives token-window truncation)
2. **File tree** — `src/` directory listing, `bin`/`obj` excluded
3. **Test file listing** — names only, with count
4. **Roslyn AST structural summary** — type declarations, base types, property and method signatures for every `.cs` file under `src/`; no method bodies
5. **Core contracts verbatim** — full source of all files under `src/Core/`

The result is cached in-memory keyed on the max `LastWriteTimeUtc` of all watched files.

## Request shape

```json
{
  "userGoal": "What would be the next phase to add to this project?",
  "fileContext": "optional additional snippet from the caller",
  "projectArea": "api"
}
```

`fileContext` is appended after the auto-generated repo context, not instead of it.

## Response shape

```json
{
  "reply": "assistant response text"
}
```

## Error responses (ProblemDetails)

| Status | Cause |
|--------|-------|
| 404 | `prompts/repo-assistant.prompty` not found |
| 429 | Model endpoint throttling; client should retry after indicated delay |
| 503 | Other upstream failure (network error, timeout, invalid response) |

## Configuration

Model settings live in `prompts/repo-assistant.prompty` (YAML front matter):

```yaml
model:
  api: chat
  configuration:
    type: openai
    base_url: http://localhost:11434/v1
    api_key: ollama
    model: gemma3:4b
  parameters:
    temperature: 0.2
    max_tokens: 800
```

Override `base_url`, `api_key`, and `model` in the prompty file or via user secrets
(`RepoAssistant:PromptPath` to point to a different template).

## Operational notes

- `HttpClient.Timeout` is set to 120 seconds to accommodate CPU-bound local models.
- Throttle retries use exponential backoff capped at 2 retries with a 2-second ceiling.
- The prompty file is hot-reloaded on change (cache keyed on `LastWriteTimeUtc`).
- Context is rebuilt on source file changes (same cache invalidation strategy).
