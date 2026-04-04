# Repo Assistant

The repository now includes a minimal prompt-driven assistant endpoint for local development and API experiments.

## What is implemented

- A prompt template at `prompts/repo-assistant.prompty`
- A Core service contract in `src/Core/Services/IRepoAssistantService.cs`
- An Infrastructure implementation in `src/Infrastructure/Services/RepoAssistantService.cs`
- An API controller at `POST /repo-assistant/run`

The current implementation reads model settings from the `.prompty` file and sends a single request to an OpenAI-compatible chat completions endpoint. This keeps the first slice small and testable without introducing additional runtime dependencies.

## Request shape

```json
{
  "userGoal": "Summarize the status endpoint",
  "fileContext": "optional file snippet",
  "projectArea": "api"
}
```

## Response shape

```json
{
  "reply": "assistant response text"
}
```

## Operational notes

- The API resolves the prompt file from the repository root so the feature works from both `dotnet run` and integration tests.
- Missing prompt templates return `404 ProblemDetails`.
- Upstream model failures return `503 ProblemDetails`.
- The default prompt configuration targets a local OpenAI-compatible endpoint at `http://localhost:11434/v1`.

## Next likely extension

If this feature grows beyond a single prompt, the next increment should introduce explicit configuration binding for model settings and richer prompt/template parsing rather than embedding all settings in the `.prompty` file.