using IncidentResponseAgent.Agent;
using IncidentResponseAgent.Application;
using IncidentResponseAgent.Application.Incidents;
using IncidentResponseAgent.Infrastructure;
using IncidentResponseAgent.Agent.Incidents;
using IncidentResponseAgent.Infrastructure.Runbooks;
using IncidentResponseAgent.Infrastructure.Tools;
using IncidentResponseAgent.Infrastructure.Incidents;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using IncidentResponseAgent.Api.Services;
using IncidentResponseAgent.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using IncidentResponseAgent.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.Configure<IncidentAnalysisAgentOptions>(builder.Configuration.GetSection("Agent:IncidentAnalysis"));
builder.Services.Configure<RunbookRetrievalOptions>(builder.Configuration.GetSection("Runbooks:SemanticRetrieval"));
builder.Services.Configure<OperationalDataOptions>(builder.Configuration.GetSection("Tools:OperationalData"));
builder.Services.Configure<IncidentStorageOptions>(builder.Configuration.GetSection("Storage:Incidents"));
builder.Services.Configure<MonitoringRuntimeOptions>(builder.Configuration.GetSection("Monitoring"));
builder.Services.Configure<ServiceCatalogOptions>(builder.Configuration.GetSection("ServiceCatalog"));
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddAgent();
builder.Services.AddSingleton<ServerIncidentMonitoringService>();
builder.Services.AddSingleton<IIncidentMonitoringCoordinator>(provider => provider.GetRequiredService<ServerIncidentMonitoringService>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<ServerIncidentMonitoringService>());
builder.Services.AddProblemDetails();
var authentication = builder.Configuration.GetSection("Authentication").Get<IncidentAuthenticationOptions>() ?? new();
builder.Services.Configure<IncidentAuthenticationOptions>(builder.Configuration.GetSection("Authentication"));
if (authentication.AllowDevelopmentIdentity)
{
    builder.Services.AddAuthentication(IncidentAuthenticationOptions.DevelopmentScheme)
        .AddScheme<AuthenticationSchemeOptions, IncidentDevelopmentAuthenticationHandler>(IncidentAuthenticationOptions.DevelopmentScheme, _ => { });
}
else
{
    if (string.IsNullOrWhiteSpace(authentication.Authority) || string.IsNullOrWhiteSpace(authentication.Audience))
        throw new InvalidOperationException("Authentication:Authority and Authentication:Audience are required when development identity is disabled.");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.Authority = authentication.Authority;
        options.Audience = authentication.Audience;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true, ValidateLifetime = true,
            NameClaimType = authentication.NameClaimType, RoleClaimType = authentication.RoleClaimType,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
}
builder.Services.AddAuthorizationBuilder().SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireAssertion(context =>
    {
        if (authentication.AllowDevelopmentIdentity || context.User.IsInRole("responder") || context.User.IsInRole("admin")) return true;
        if (string.IsNullOrWhiteSpace(authentication.RequiredScope)) return true;
        var scopes = context.User.FindAll("scp").Concat(context.User.FindAll("scope")).SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return scopes.Contains(authentication.RequiredScope, StringComparer.Ordinal);
    })
    .Build());

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

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
    {
        status = "Healthy",
        version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"
    }))
    .WithName("Health");

app.MapControllers();

app.Run();

static (int StatusCode, string Title, string Detail) MapException(Exception? exception)
{
    return exception switch
    {
        IncidentAnalysisUnavailableException unavailableException => (
            StatusCodes.Status503ServiceUnavailable,
            "Incident analysis unavailable",
            unavailableException.Message),
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
