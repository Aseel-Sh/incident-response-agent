using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace IncidentResponseAgent.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequestBudgetAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

public static class DeploymentSecurity
{
    public const long MaxBodyBytes = 65_536;

    public static void AddDeploymentSecurity(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = MaxBodyBytes;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
            options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
        });
        builder.Services.AddRequestTimeouts(options => options.AddPolicy("request", TimeSpan.FromSeconds(240)));
        var keys = builder.Services.AddDataProtection().SetApplicationName("IncidentResponseAgent");
        if (config["Storage:DataProtectionKeysPath"] is { Length: > 0 } path)
            keys.PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(path)));
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            // Never trust arbitrary senders or X-Forwarded-Host.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var proxy in config.GetSection("Security:KnownProxies").Get<string[]>() ?? [])
                options.KnownProxies.Add(IPAddress.Parse(proxy));
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                    ? Math.Ceiling(retry.TotalSeconds).ToString(CultureInfo.InvariantCulture) : "1";
                await context.HttpContext.Response.WriteAsJsonAsync(new { error = "Request budget exceeded. Retry later." }, token);
            };
            string Budget(HttpContext context) => context.GetEndpoint()?.Metadata.GetMetadata<RequestBudgetAttribute>()?.Name ?? "General";
            bool Exempt(HttpContext context) => context.Request.Path == "/health";
            int Limit(string budget) => config.GetValue<int?>("RateLimits:" + budget) is { } value
                ? value > 0 ? value : throw new InvalidOperationException("Rate limits must be positive.")
                : budget switch { "Expensive" => 6, "Signals" => 60, "Readiness" => 12, _ => 120 };
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(context => Exempt(context)
                    ? RateLimitPartition.GetNoLimiter("health")
                    : RateLimitPartition.GetFixedWindowLimiter(Budget(context) + ":" + (context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"), _ => new()
                    {
                        PermitLimit = Limit(Budget(context)), Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                    })),
                PartitionedRateLimiter.Create<HttpContext, string>(context => Exempt(context)
                    ? RateLimitPartition.GetNoLimiter("health")
                    : RateLimitPartition.GetFixedWindowLimiter(Budget(context), _ => new()
                    {
                        PermitLimit = checked(Limit(Budget(context)) * 5), Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                    })),
                PartitionedRateLimiter.Create<HttpContext, string>(context => Exempt(context)
                    ? RateLimitPartition.GetNoLimiter("health")
                    : RateLimitPartition.GetConcurrencyLimiter(Budget(context), _ => new()
                    {
                        PermitLimit = Budget(context) == "Expensive" ? 2 : 32, QueueLimit = 0
                    })));
        });
    }

    public static void UseDeploymentSecurity(this WebApplication app)
    {
        // An empty explicit list means forwarding is disabled, not trust-all.
        if (app.Configuration.GetSection("Security:KnownProxies").GetChildren().Any()) app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) app.UseHsts();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
            if (context.Request.ContentLength > MaxBodyBytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }
            await next();
        });
    }
}
