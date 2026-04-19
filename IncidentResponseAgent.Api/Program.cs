using IncidentResponseAgent.Agent;
using IncidentResponseAgent.Application;
using IncidentResponseAgent.Infrastructure;
using IncidentResponseAgent.Agent.Incidents;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.Configure<IncidentAnalysisAgentOptions>(builder.Configuration.GetSection("Agent:IncidentAnalysis"));
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddAgent();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("Health");

app.MapControllers();

app.Run();
