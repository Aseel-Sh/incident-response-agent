using IncidentResponseAgent.Agent;
using IncidentResponseAgent.Application;
using IncidentResponseAgent.Infrastructure;
using IncidentResponseAgent.Agent.Incidents;
using IncidentResponseAgent.Infrastructure.Runbooks;
using IncidentResponseAgent.Infrastructure.Tools;
using IncidentResponseAgent.Infrastructure.Incidents;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.Configure<IncidentAnalysisAgentOptions>(builder.Configuration.GetSection("Agent:IncidentAnalysis"));
builder.Services.Configure<RunbookRetrievalOptions>(builder.Configuration.GetSection("Runbooks:SemanticRetrieval"));
builder.Services.Configure<OperationalDataOptions>(builder.Configuration.GetSection("Tools:OperationalData"));
builder.Services.Configure<IncidentStorageOptions>(builder.Configuration.GetSection("Storage:Incidents"));
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddAgent();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var (statusCode, title, detail) = MapException(exception);
        context.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;
        await context.Response.WriteAsJsonAsync(problem);
    });
});

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

static (int StatusCode, string Title, string Detail) MapException(Exception? exception)
{
    return exception switch
    {
        ArgumentException argumentException => (
            StatusCodes.Status400BadRequest,
            "Invalid request",
            argumentException.Message),
        InvalidOperationException invalidOperationException => (
            StatusCodes.Status503ServiceUnavailable,
            "Service is not configured",
            invalidOperationException.Message),
        HttpRequestException httpRequestException => (
            StatusCodes.Status503ServiceUnavailable,
            "External service is unavailable",
            httpRequestException.Message),
        _ when exception?.GetType().Name.Contains("ClientResultException", StringComparison.OrdinalIgnoreCase) == true => (
            StatusCodes.Status502BadGateway,
            "Model provider request failed",
            "The configured model provider rejected or failed the request. Check API key, model access, and provider status."),
        _ => (
            StatusCodes.Status500InternalServerError,
            "Unexpected server error",
            "The server could not complete the request. Check application logs for details.")
    };
}
