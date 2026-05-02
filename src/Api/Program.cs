using Api.Middleware;
using Core.Services;
using Infrastructure.Services;
using Microsoft.SemanticKernel;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(
    (context, services, configuration) =>
    {
        var logPath = Path.Combine(
            context.HostingEnvironment.ContentRootPath,
            "logs",
            "assistant-.log"
        );
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
            );
    }
);

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
    var configuration = services.GetRequiredService<IConfiguration>();

    var configuredPromptPath = configuration["RepoAssistant:PromptPath"];
    var promptPath = Path.IsPathRooted(configuredPromptPath)
        ? configuredPromptPath
        : Path.GetFullPath(
            Path.Combine(
                environment.ContentRootPath,
                string.IsNullOrWhiteSpace(configuredPromptPath)
                    ? Path.Combine("prompts", "repo-assistant.prompty")
                    : configuredPromptPath
            )
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

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

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
