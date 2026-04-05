using Core.Services;
using Infrastructure.Services;
using Microsoft.SemanticKernel;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<IHealthStatusService, HealthStatusService>();
builder.Services.AddHttpClient(
    "RepoAssistant",
    client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    }
);

builder.Services.AddTransient<IRepoAssistantService>(services =>
{
    var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
    var environment = services.GetRequiredService<IHostEnvironment>();
    var promptPath = Path.GetFullPath(
        Path.Combine(environment.ContentRootPath, "..", "..", "prompts", "repo-assistant.prompty")
    );

    return new RepoAssistantService(httpClientFactory.CreateClient("RepoAssistant"), promptPath);
});

var semanticKernelModelId = builder.Configuration["SemanticKernel:ModelId"];
var semanticKernelApiKey = builder.Configuration["SemanticKernel:ApiKey"];

if (
    !string.IsNullOrWhiteSpace(semanticKernelModelId)
    && !string.IsNullOrWhiteSpace(semanticKernelApiKey)
)
{
    builder.Services.AddOpenAIChatCompletion(
        modelId: semanticKernelModelId,
        apiKey: semanticKernelApiKey
    );

    builder.Services.AddScoped<IChatService, SemanticKernelChatService>();
}
else
{
    builder.Services.AddScoped<IChatService, UnconfiguredChatService>();
}

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();

app.Run();

// Make Program accessible for testing
public partial class Program { }
