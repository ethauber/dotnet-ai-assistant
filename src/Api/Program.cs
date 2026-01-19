using Api.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<IHealthStatusService, HealthStatusService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/status", (IHealthStatusService healthService) => 
{
    var status = healthService.GetStatus();
    return Results.Ok(new { status });
})
.WithName("GetStatus");

app.Run();

// Make Program accessible for testing
public partial class Program { }
