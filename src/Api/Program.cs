using Core.Services;
using Infrastructure.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<IHealthStatusService, HealthStatusService>();
builder.Services.AddTransient<IRepoAssistantService>(services =>
{
    var environment = services.GetRequiredService<IHostEnvironment>();
    var promptPath = Path.GetFullPath(
        Path.Combine(environment.ContentRootPath, "..", "..", "prompts", "repo-assistant.prompty")
    );

    return new RepoAssistantService(new HttpClient(), promptPath);
});

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
