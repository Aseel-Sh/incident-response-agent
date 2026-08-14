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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Antiforgery;
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
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
var authentication = builder.Configuration.GetSection("Authentication").Get<IncidentAuthenticationOptions>() ?? new();
var browserOidcEnabled = !authentication.AllowDevelopmentIdentity
    && !string.IsNullOrWhiteSpace(authentication.BrowserClientId)
    && !string.IsNullOrWhiteSpace(authentication.BrowserClientSecret);
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
    var authenticationBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "IncidentSmart";
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    }).AddPolicyScheme("IncidentSmart", "Bearer token or browser session", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? JwtBearerDefaults.AuthenticationScheme
                : browserOidcEnabled ? CookieAuthenticationDefaults.AuthenticationScheme : JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer(options =>
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
    if (browserOidcEnabled)
    {
        authenticationBuilder.AddCookie(options =>
        {
            options.Cookie.Name = "__Host-IncidentResponseSession";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        }).AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.Authority = authentication.Authority;
            options.ClientId = authentication.BrowserClientId;
            options.ClientSecret = authentication.BrowserClientSecret;
            options.ResponseType = "code";
            options.UsePkce = true;
            options.UseTokenLifetime = true;
            options.MapInboundClaims = false;
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.TokenValidationParameters.NameClaimType = authentication.NameClaimType;
            options.TokenValidationParameters.RoleClaimType = authentication.RoleClaimType;
            options.Scope.Clear();
            foreach (var scope in authentication.BrowserScope.Split(' ', StringSplitOptions.RemoveEmptyEntries)) options.Scope.Add(scope);
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.SetParameter("audience", authentication.Audience);
                return Task.CompletedTask;
            };
        });
    }
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
        if (exception is not null)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("IncidentResponseAgent.Api.ExceptionHandler");
            logger.LogError(exception, "Unhandled request failure. TraceId={TraceId} Path={Path} StatusCode={StatusCode}", context.TraceIdentifier, context.Request.Path, statusCode);
        }
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
app.Use(async (context, next) =>
{
    var usesBrowserCookie = context.User.Identities.Any(identity => identity.IsAuthenticated && identity.AuthenticationType == CookieAuthenticationDefaults.AuthenticationScheme);
    var unsafeMethod = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method) && !HttpMethods.IsTrace(context.Request.Method);
    if (usesBrowserCookie && unsafeMethod)
    {
        await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
    }
    await next();
});
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
    {
        status = "Healthy",
        version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"
    }))
    .WithName("Health");

app.MapGet("/auth/session", (HttpContext context, IAntiforgery antiforgery) =>
{
    var cookieIdentity = context.User.Identities.FirstOrDefault(identity => identity.IsAuthenticated && identity.AuthenticationType == CookieAuthenticationDefaults.AuthenticationScheme);
    var csrfToken = cookieIdentity is null ? null : antiforgery.GetAndStoreTokens(context).RequestToken;
    var roleClaimType = (context.User.Identity as System.Security.Claims.ClaimsIdentity)?.RoleClaimType ?? System.Security.Claims.ClaimTypes.Role;
    return Results.Ok(new
    {
        browserLoginEnabled = browserOidcEnabled,
        authenticated = context.User.Identity?.IsAuthenticated == true,
        name = context.User.Identity?.Name,
        roles = context.User.Claims.Where(claim => claim.Type == roleClaimType).Select(claim => claim.Value).ToArray(),
        csrfToken
    });
}).AllowAnonymous();

app.MapGet("/auth/login", (string? returnUrl) =>
{
    if (!browserOidcEnabled) return Results.Problem("Browser OIDC login requires Authentication:BrowserClientId and Authentication:BrowserClientSecret.", statusCode: StatusCodes.Status503ServiceUnavailable);
    var safeReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") ? returnUrl : "/";
    return Results.Challenge(new AuthenticationProperties { RedirectUri = safeReturnUrl }, [OpenIdConnectDefaults.AuthenticationScheme]);
}).AllowAnonymous();

app.MapPost("/auth/logout", () => browserOidcEnabled
    ? Results.SignOut(new AuthenticationProperties { RedirectUri = "/" }, [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme])
    : Results.Redirect("/"));

app.MapControllers();

app.Run();

static (int StatusCode, string Title, string Detail) MapException(Exception? exception)
{
    return exception switch
    {
        AntiforgeryValidationException antiforgeryException => (
            StatusCodes.Status400BadRequest,
            "Invalid antiforgery token",
            antiforgeryException.Message),
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

public partial class Program;
