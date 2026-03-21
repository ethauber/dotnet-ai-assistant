# PLAN-001 — Semantic Kernel Hello World

<!-- STATUS: Draft -->

## Overview

Add a minimal `/chat` endpoint backed by Microsoft Semantic Kernel (SK) for chat
completion. This introduces SK into the Clean Architecture layer stack without
replacing the existing AWS Bedrock adapter—both can coexist.

**Target:** POST `/chat` accepts `{ "message": "..." }` and returns `{ "reply": "..." }`.

## Prerequisites

- Solution builds and all existing tests pass (`dotnet test`)
- An OpenAI API key (or Azure OpenAI deployment) available for local testing
- .NET 10 SDK installed

---

## Steps

### 1 — Add NuGet packages

- [ ] Add Semantic Kernel to `Infrastructure`
  ```bash
  dotnet add src/Infrastructure/Infrastructure.csproj package Microsoft.SemanticKernel
  ```
- [ ] Add Semantic Kernel to `Tests` (for mocking `IChatCompletionService`)
  ```bash
  dotnet add tests/Tests/Tests.csproj package Microsoft.SemanticKernel
  ```
- [ ] Verify build still passes
  ```bash
  dotnet build
  ```

---

### 2 — Define `IChatService` in Core

- [ ] Create `src/Core/Services/IChatService.cs`
  ```csharp
  namespace Core.Services;

  public interface IChatService
  {
      Task<string> ChatAsync(string userMessage, CancellationToken cancellationToken = default);
  }
  ```

---

### 3 — Implement `SemanticKernelChatService` in Infrastructure

- [ ] Create `src/Infrastructure/Services/SemanticKernelChatService.cs`
  ```csharp
  using Core.Services;
  using Microsoft.SemanticKernel.ChatCompletion;

  namespace Infrastructure.Services;

  public class SemanticKernelChatService : IChatService
  {
      private readonly IChatCompletionService _chatCompletion;

      public SemanticKernelChatService(IChatCompletionService chatCompletion)
      {
          _chatCompletion = chatCompletion;
      }

      public async Task<string> ChatAsync(string userMessage, CancellationToken cancellationToken = default)
      {
          var history = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
          history.AddUserMessage(userMessage);

          var result = await _chatCompletion.GetChatMessageContentAsync(
              history,
              cancellationToken: cancellationToken);

          return result.Content ?? string.Empty;
      }
  }
  ```

---

### 4 — Add request/response DTOs to Api

- [ ] Create `src/Api/Models/ChatRequest.cs`
  ```csharp
  namespace Api.Models;

  public record ChatRequest(string Message);
  ```
- [ ] Create `src/Api/Models/ChatResponse.cs`
  ```csharp
  namespace Api.Models;

  public record ChatResponse(string Reply);
  ```

---

### 5 — Add `ChatController` to Api

- [ ] Create `src/Api/Controllers/ChatController.cs`
  ```csharp
  using Api.Models;
  using Core.Services;
  using Microsoft.AspNetCore.Mvc;

  namespace Api.Controllers;

  [ApiController]
  [Route("chat")]
  public class ChatController : ControllerBase
  {
      private readonly IChatService _chatService;

      public ChatController(IChatService chatService)
      {
          _chatService = chatService;
      }

      /// <summary>Sends a message and returns the assistant reply.</summary>
      /// <response code="200">Returns the assistant reply.</response>
      /// <response code="400">Request body is invalid.</response>
      [HttpPost]
      [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
      [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
      public async Task<ActionResult<ChatResponse>> Post(
          [FromBody] ChatRequest request,
          CancellationToken cancellationToken)
      {
          var reply = await _chatService.ChatAsync(request.Message, cancellationToken);
          return Ok(new ChatResponse(reply));
      }
  }
  ```

---

### 6 — Wire up DI in `Program.cs`

- [ ] Add the OpenAI chat completion service and register `IChatService`

  In `src/Api/Program.cs`, after the existing `AddSingleton<IHealthStatusService>` line:
  ```csharp
  // Semantic Kernel — swap AddOpenAIChatCompletion for AddAzureOpenAIChatCompletion
  // or the Amazon Bedrock connector as needed.
  builder.Services.AddOpenAIChatCompletion(
      modelId: builder.Configuration["SemanticKernel:ModelId"]!,
      apiKey: builder.Configuration["SemanticKernel:ApiKey"]!);

  builder.Services.AddScoped<IChatService, SemanticKernelChatService>();
  ```

  Add the required `using` at the top:
  ```csharp
  using Microsoft.SemanticKernel;
  ```

---

### 7 — Add configuration

- [ ] Add the SK config block to `src/Api/appsettings.json`:
  ```json
  "SemanticKernel": {
    "ModelId": "gpt-4o-mini",
    "ApiKey": ""
  }
  ```
- [ ] Store the real API key in User Secrets (never commit it):
  ```bash
  dotnet user-secrets init --project src/Api
  dotnet user-secrets set "SemanticKernel:ApiKey" "<your-openai-api-key>" --project src/Api
  ```

---

### 8 — Add a unit test

- [ ] Create `tests/Tests/ChatControllerTests.cs`
  ```csharp
  using Api.Controllers;
  using Api.Models;
  using Core.Services;
  using FluentAssertions;
  using Microsoft.AspNetCore.Mvc;
  using Moq;

  namespace Tests;

  public class ChatControllerTests
  {
      [Fact]
      public async Task Post_ReturnsReplyFromChatService()
      {
          var mockService = new Mock<IChatService>();
          mockService
              .Setup(s => s.ChatAsync("hello", It.IsAny<CancellationToken>()))
              .ReturnsAsync("world");

          var controller = new ChatController(mockService.Object);

          var result = await controller.Post(new ChatRequest("hello"), CancellationToken.None);

          var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
          ok.Value.Should().BeEquivalentTo(new ChatResponse("world"));
      }
  }
  ```
- [ ] Add Moq to the Tests project (if not already present):
  ```bash
  dotnet add tests/Tests/Tests.csproj package Moq
  ```

---

### 9 — Run tests

- [ ] All tests pass:
  ```bash
  dotnet test
  ```

---

## Verification

- [ ] Start the API:
  ```bash
  dotnet run --project src/Api
  ```
- [ ] Send a test request:
  ```bash
  curl -s -X POST http://localhost:5176/chat \
    -H "Content-Type: application/json" \
    -d '{"message": "Say hello in one sentence"}' | jq .
  ```
  Expected shape:
  ```json
  { "reply": "Hello! How can I assist you today?" }
  ```
- [ ] OpenAPI schema visible at `http://localhost:5176/openapi/v1.json` and includes `/chat`

---

## Notes

- To swap to the **Amazon Bedrock** connector instead of OpenAI, replace
  `AddOpenAIChatCompletion` with the Amazon connector registration from
  `Microsoft.SemanticKernel.Connectors.Amazon` and update config accordingly.
  The `IChatService` / `SemanticKernelChatService` code is unchanged.
- Conversation history management (multi-turn) belongs in a dedicated `Core`
  service per `.github/copilot-instructions.md` section 3 — that's a future plan.
