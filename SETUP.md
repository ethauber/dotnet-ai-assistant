# dotnet-ai-assistant Setup

## CLI Commands for Setup

### 1. Create Solution
```bash
dotnet new sln -n dotnet-ai-assistant
```

### 2. Create Projects
```bash
mkdir -p src tests
dotnet new webapi -n Api -o src/Api --no-https
dotnet new classlib -n Core -o src/Core
dotnet new classlib -n Infrastructure -o src/Infrastructure
dotnet new xunit -n Tests -o tests/Tests
```

### 3. Add Projects to Solution
```bash
dotnet sln add src/Api/Api.csproj
dotnet sln add src/Core/Core.csproj
dotnet sln add src/Infrastructure/Infrastructure.csproj
dotnet sln add tests/Tests/Tests.csproj
```

### 4. Add Project References
```bash
dotnet add src/Api/Api.csproj reference src/Core/Core.csproj
dotnet add src/Api/Api.csproj reference src/Infrastructure/Infrastructure.csproj
dotnet add src/Infrastructure/Infrastructure.csproj reference src/Core/Core.csproj
dotnet add tests/Tests/Tests.csproj reference src/Core/Core.csproj
dotnet add tests/Tests/Tests.csproj reference src/Infrastructure/Infrastructure.csproj
```

### 5. Add NuGet Packages
```bash
dotnet add src/Infrastructure/Infrastructure.csproj package AWSSDK.BedrockRuntime
dotnet add tests/Tests/Tests.csproj package FluentAssertions
```

### 6. Build Solution
```bash
dotnet build
```

### 7. Run Tests
```bash
dotnet test
```

## Project Structure
```
dotnet-ai-assistant/
├── src/
│   ├── Api/           # ASP.NET Core Web API
│   ├── Core/          # Core domain models and interfaces
│   └── Infrastructure/ # External dependencies (AWS Bedrock)
├── tests/
│   └── Tests/         # xUnit tests with FluentAssertions
└── dotnet-ai-assistant.sln
```

## BedrockConverseAdapter

The `BedrockConverseAdapter` class in the `Infrastructure` project builds a `ConverseRequest` for AWS Bedrock with strict inference parameters (Temperature = 0).

### Test: Request_Should_Have_Strict_Inference_Params

This test verifies that the `ConverseRequest` built by `BedrockConverseAdapter` has:
- Non-null `InferenceConfig`
- Temperature set to 0 (for deterministic/strict inference)
