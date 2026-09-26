using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using IncidentResponseAgent.Api.Services;
using IncidentResponseAgent.Infrastructure.Runbooks;

namespace IncidentResponseAgent.Tests;

public sealed class DeploymentSecurityTests
{
    private static Claim[] Responder(string subject) => [new("sub", subject), new("https://incidentresponseagent/roles", "responder")];

    [Fact]
    public async Task ExpensiveBudgetRejectsSeventhRequestButLeavesSignalAndReadBudgetsAvailable()
    {
        using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateAuthenticatedClient(Responder("limited"));
        // Invalid input exercises the real pipeline without invoking external providers.
        for (var i = 0; i < 6; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/incidents/analyze", new { })).StatusCode);
        var limited = await client.PostAsJsonAsync("/api/incidents/analyze", new { });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/signals/logs", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/identity/me")).StatusCode);
        using var second = factory.CreateAuthenticatedClient(Responder("other"));
        Assert.Equal(HttpStatusCode.BadRequest, (await second.PostAsJsonAsync("/api/incidents/analyze", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task SharedExpensiveBudgetCannotBeBypassedByRotatingSubjects()
    {
        using var factory = new AuthenticationApiFactory();
        for (var subject = 0; subject < 5; subject++)
        {
            using var client = factory.CreateAuthenticatedClient(Responder("subject-" + subject));
            for (var request = 0; request < 6; request++)
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/incidents/analyze", new { })).StatusCode);
        }
        using var next = factory.CreateAuthenticatedClient(Responder("new-subject"));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await next.PostAsJsonAsync("/api/incidents/analyze", new { })).StatusCode);
    }

    [Fact]
    public async Task AuthorizedRespondersShareProjectCandidatesByDesign()
    {
        using var factory = new AuthenticationApiFactory();
        using var creator = factory.CreateAuthenticatedClient(Responder("creator"));
        var created = await creator.PostAsJsonAsync("/api/incidents/candidates/manual", new
        {
            title = "Shared workspace example", description = "Synthetic authorization test", severity = "sev2", projectId = "other-project"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var reader = factory.CreateAuthenticatedClient(Responder("second-responder"));
        var candidates = await reader.GetStringAsync("/api/incidents/detected?projectId=other-project");
        Assert.Contains("Shared workspace example", candidates);
    }

    [Fact]
    public async Task SignalBudgetRejectsAfterSixtyRequests()
    {
        using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateAuthenticatedClient(Responder("ingester"));
        for (var i = 0; i < 60; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/signals/metrics", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/signals/metrics", new { })).StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/incidents/candidates/other-project-candidate/confirm")]
    [InlineData("PUT", "/api/incidents/11111111-1111-1111-1111-111111111111/status")]
    [InlineData("POST", "/api/incidents/11111111-1111-1111-1111-111111111111/analysis/retry")]
    [InlineData("GET", "/api/incidents/recent?projectId=other-project")]
    [InlineData("POST", "/api/signals/logs")]
    public async Task ObjectRoutesRejectUnauthenticatedAndUnauthorizedUsers(string method, string path)
    {
        using var factory = new AuthenticationApiFactory();
        using var anonymous = factory.CreateClient();
        using var denied = factory.CreateAuthenticatedClient(new Claim("sub", "viewer"));
        foreach (var (client, status) in new[] { (anonymous, HttpStatusCode.Unauthorized), (denied, HttpStatusCode.Forbidden) })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { projectId = "other-project" }) };
            Assert.Equal(status, (await client.SendAsync(request)).StatusCode);
        }
    }

    [Fact]
    public async Task BodyLimitAndSecurityHeadersApplyBeforeControllerExecution()
    {
        using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateAuthenticatedClient(Responder("body-limit"));
        var response = await client.PostAsync("/api/incidents/analyze", new StringContent(new string('x', 65_537), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        var health = await client.GetAsync("/health");
        Assert.Equal("nosniff", health.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("frame-ancestors 'none'", health.Headers.GetValues("Content-Security-Policy").Single());
        Assert.False(health.Headers.Contains("Access-Control-Allow-Origin"));
        using var badHost = new HttpRequestMessage(HttpMethod.Get, "/health");
        badHost.Headers.Host = "attacker.invalid";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(badHost)).StatusCode);
    }

    [Fact]
    public async Task UntrustedForwardingCannotChangeTransportScheme()
    {
        using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Forwarded-Proto", "http");
        request.Headers.Add("X-Forwarded-Host", "attacker.invalid");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task ConfiguredDependenciesNeverImplyVerifiedReadiness()
    {
        using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateAuthenticatedClient(Responder("readiness"));
        var response = await client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("components").GetProperty("model").GetProperty("executionVerified").GetBoolean());
        Assert.NotEqual("ready", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task MerelyConfiguredModelRemainsUnverified()
    {
        using var factory = new AuthenticationApiFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.UseSetting("Agent:IncidentAnalysis:ApiKey", "synthetic-not-a-credential"));
        using var signed = factory.CreateAuthenticatedClient(Responder("configured-check"));
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = signed.DefaultRequestHeaders.Authorization;
        var response = await client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var model = json.RootElement.GetProperty("components").GetProperty("model");
        Assert.Equal("configured", model.GetProperty("status").GetString());
        Assert.False(model.GetProperty("executionVerified").GetBoolean());
    }

    [Fact]
    public void PersistenceProbeRequiresActualWritableDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "ira-readiness-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Equal("reachable", DependencyReadinessProbe.Persistence([Path.Combine(root, "state.json")]));
            Assert.Empty(Directory.GetFiles(root));
            File.WriteAllText(Path.Combine(root, "file"), "synthetic");
            Assert.Equal("unavailable", DependencyReadinessProbe.Persistence([Path.Combine(root, "file", "state.json")]));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "reachable")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "unavailable")]
    [InlineData(HttpStatusCode.Unauthorized, "unavailable")]
    [InlineData(HttpStatusCode.Redirect, "unavailable")]
    public async Task QdrantProbeUsesActualHttpResult(HttpStatusCode code, string expected)
    {
        var probe = new DependencyReadinessProbe(new ProbeClientFactory(new ProbeHandler(code)));
        Assert.Equal(expected, await probe.QdrantAsync(new RunbookRetrievalOptions { QdrantEndpoint = "https://qdrant.test" }, default));
    }

    [Fact]
    public async Task QdrantProbeBoundsAnUnresponsiveDependency()
    {
        var probe = new DependencyReadinessProbe(new ProbeClientFactory(new ProbeHandler(null)));
        var result = await probe.QdrantAsync(new RunbookRetrievalOptions { QdrantEndpoint = "https://qdrant.test" }, default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("unavailable", result);
    }

    private sealed class ProbeClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }
    private sealed class ProbeHandler(HttpStatusCode? code) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal("/readyz", request.RequestUri!.AbsolutePath);
            if (code is null) await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(code!.Value);
        }
    }
}
