using Api;
using Api.Middleware;
using Core.Services;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

SelfLog.Enable(msg => Console.Error.WriteLine("[serilog-selflog] {0}", msg));

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(
    (context, services, configuration) =>
    {
        var logPath = Path.Combine(
            context.HostingEnvironment.ContentRootPath,
            "logs",
            "assistant-.log"
        );
        var sqliteDbPath = Path.Combine(context.HostingEnvironment.ContentRootPath, "assistant.db");
        var batchingSink = new PeriodicBatchingSink(
            new SQLiteLogSink(sqliteDbPath),
            new PeriodicBatchingSinkOptions
            {
                BatchSizeLimit = 50,
                Period = TimeSpan.FromSeconds(2),
            }
        );

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
            )
            .WriteTo.Logger(lc =>
                lc.Filter.ByIncludingOnly(e =>
                        e.Level >= LogEventLevel.Information
                        && e.Properties.TryGetValue("SourceContext", out var sc)
                        && sc is ScalarValue { Value: string ctx }
                        && (
                            ctx.StartsWith("Api.", StringComparison.Ordinal)
                            || ctx.StartsWith("Infrastructure.", StringComparison.Ordinal)
                            || ctx.StartsWith("Core.", StringComparison.Ordinal)
                        )
                    )
                    .WriteTo.Sink(batchingSink)
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
        client.Timeout = TimeSpan.FromSeconds(120);
    }
);

builder.Services.AddTransient<IRepoAssistantService>(services =>
{
    var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
    var environment = services.GetRequiredService<IHostEnvironment>();

    var promptsDir = Path.GetFullPath(
        Path.Combine(environment.ContentRootPath, "..", "..", "prompts")
    );

    return new RepoAssistantService(
        httpClientFactory.CreateClient("RepoAssistant"),
        promptsDir,
        services.GetRequiredService<ILogger<RepoAssistantService>>(),
        repoRootPath: Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."))
    );
});

builder.Services.AddSingleton<IPromptTemplateService>(services =>
{
    var environment = services.GetRequiredService<IHostEnvironment>();
    var promptsDir = Path.GetFullPath(
        Path.Combine(environment.ContentRootPath, "..", "..", "prompts")
    );
    return new PromptTemplateService(promptsDir);
});

var semanticKernelModelId = builder.Configuration["SemanticKernel:ModelId"];
var semanticKernelApiKey = builder.Configuration["SemanticKernel:ApiKey"];
var semanticKernelEndpoint = builder.Configuration["SemanticKernel:Endpoint"];

if (
    !string.IsNullOrWhiteSpace(semanticKernelModelId)
    && !string.IsNullOrWhiteSpace(semanticKernelApiKey)
)
{
    if (!string.IsNullOrWhiteSpace(semanticKernelEndpoint))
    {
        builder.Services.AddOpenAIChatCompletion(
            modelId: semanticKernelModelId,
            apiKey: semanticKernelApiKey,
            endpoint: new Uri(semanticKernelEndpoint)
        );
    }
    else
    {
        builder.Services.AddOpenAIChatCompletion(
            modelId: semanticKernelModelId,
            apiKey: semanticKernelApiKey
        );
    }

    builder.Services.AddScoped<IChatService, SemanticKernelChatService>();
}
else
{
    builder.Services.AddScoped<IChatService, UnconfiguredChatService>();
}

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "assistant.db");
builder.Services.AddDbContext<AssistantDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}")
);
builder.Services.AddScoped<IAssistantRunRepository, AssistantRunRepository>();
builder.Services.AddScoped<IAssistantRunService, AssistantRunService>();

builder.Services.AddRazorPages();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AssistantDbContext>().Database.Migrate();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseWhen(
    context =>
    {
        var path = context.Request.Path;
        // Only apply status code pages to Razor Page routes (not API controllers).
        return !path.StartsWithSegments("/assistant-runs")
            && !path.StartsWithSegments("/chat")
            && !path.StartsWithSegments("/repo-assistant")
            && !path.StartsWithSegments("/status");
    },
    appBuilder => appBuilder.UseStatusCodePagesWithReExecute("/error/{0}")
);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.MapRazorPages();

app.Run();

// Make Program accessible for testing
public partial class Program { }
