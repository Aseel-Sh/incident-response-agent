using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace IncidentResponseAgent.Tests;

public sealed class AuthenticationIntegrationTests : IClassFixture<AuthenticationApiFactory>
{
	private const string Auth0RoleClaim = "https://incidentresponseagent/roles";
	private readonly AuthenticationApiFactory _factory;

	public AuthenticationIntegrationTests(AuthenticationApiFactory factory) => _factory = factory;

	[Theory]
	[InlineData("/api/identity/me")]
	[InlineData("/api/responders")]
	[InlineData("/api/projects")]
	[InlineData("/api/services")]
	[InlineData("/api/incidents/recent")]
	[InlineData("/api/operations/sources")]
	[InlineData("/api/runbooks/sources")]
	[InlineData("/api/monitoring/state")]
	[InlineData("/ready")]
	public async Task AnonymousRequestsCannotReadWorkspaceData(string path)
	{
		using var client = _factory.CreateClient();
		var response = await client.GetAsync(path);
		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
	}

	[Fact]
	public async Task SignedInUserWithoutResponderPermissionCannotEnterWorkspace()
	{
		using var client = _factory.CreateAuthenticatedClient(new Claim("name", "unassigned-user"));
		Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/projects")).StatusCode);
		using var session = JsonDocument.Parse(await client.GetStringAsync("/auth/session"));
		Assert.True(session.RootElement.GetProperty("authenticated").GetBoolean());
		Assert.False(session.RootElement.GetProperty("canAccess").GetBoolean());
	}

	[Theory]
	[InlineData("/api/projects")]
	[InlineData("/api/incidents/analyze")]
	[InlineData("/api/signals/logs")]
	public async Task AnonymousRequestsCannotMutateWorkspace(string path)
	{
		using var client = _factory.CreateClient();
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(path, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
	}

	[Fact]
	public void ProductionRejectsAutomaticDevelopmentIdentity()
	{
		using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production").UseSetting("Authentication:AllowDevelopmentIdentity", "true"));
		Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
	}

	[Fact]
	public async Task InvalidBearerTokenIsRejected()
	{
		using var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

		var response = await client.GetAsync("/api/identity/me");

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task BrowserSessionEndpointReportsLoginAsDisabledWhenNoClientIsConfigured()
	{
		using var client = _factory.CreateClient();

		var response = await client.GetAsync("/auth/session");
		var body = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("\"browserLoginEnabled\":false", body);
		Assert.Contains("\"authenticated\":false", body);
	}

	[Fact]
	public async Task ValidResponderRoleCanUseProtectedApi()
	{
		using var client = _factory.CreateAuthenticatedClient(new Claim("name", "response-lead"), new Claim(Auth0RoleClaim, "responder"));

		var response = await client.GetAsync("/api/identity/me");
		var body = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("response-lead", body);
		Assert.Contains("responder", body);
	}

	[Fact]
	public async Task RequiredDelegatedScopeCanUseProtectedApi()
	{
		using var client = _factory.CreateAuthenticatedClient(new Claim("name", "delegated-responder"), new Claim("scp", "openid incident.respond"));

		var response = await client.GetAsync("/api/identity/me");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task ResponderRoleCannotUseAdminApi()
	{
		using var client = _factory.CreateAuthenticatedClient(new Claim("name", "response-lead"), new Claim(Auth0RoleClaim, "responder"));

		var response = await client.DeleteAsync("/api/projects/project-that-does-not-exist");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Theory]
	[InlineData("POST", "/api/projects")]
	[InlineData("POST", "/api/runbooks/sources")]
	[InlineData("PUT", "/api/runbooks/sources/test/enabled")]
	[InlineData("POST", "/api/runbooks/sources/test/synchronize")]
	[InlineData("DELETE", "/api/runbooks/sources/test")]
	[InlineData("DELETE", "/api/incidents/11111111-1111-1111-1111-111111111111")]
	[InlineData("POST", "/api/incidents/11111111-1111-1111-1111-111111111111/knowledge-review")]
	public async Task AllAdministrativeMutationsRejectResponder(string method, string path)
	{
		using var client = _factory.CreateAuthenticatedClient(new Claim("sub", "test-responder"), new Claim("name", "Responder"), new Claim(Auth0RoleClaim, "responder"));
		using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
		Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
	}

	[Fact]
	public async Task DirectoryRegistersStableIdentityOnlyForAuthorizedResponders()
	{
		using var responder = _factory.CreateAuthenticatedClient(new Claim("sub", "auth0|directory-test"), new Claim("name", "Test Responder"), new Claim(Auth0RoleClaim, "responder"));
		using var session = JsonDocument.Parse(await responder.GetStringAsync("/auth/session"));
		Assert.Equal("auth0|directory-test", session.RootElement.GetProperty("userId").GetString());
		using var denied = _factory.CreateAuthenticatedClient(new Claim("sub", "auth0|denied-test"), new Claim("name", "Denied"));
		await denied.GetAsync("/auth/session");
		var directory = await responder.GetStringAsync("/api/responders");
		Assert.Contains("auth0|directory-test", directory);
		Assert.DoesNotContain("auth0|denied-test", directory);
	}

	[Fact]
	public async Task AdminRoleCanReachAdminApi()
	{
		using var client = _factory.CreateAuthenticatedClient(new Claim("name", "platform-admin"), new Claim(Auth0RoleClaim, "admin"));

		var response = await client.DeleteAsync("/api/projects/project-that-does-not-exist");

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}
}

public sealed class Auth0BrowserAuthenticationIntegrationTests : IClassFixture<Auth0BrowserApiFactory>
{
	private readonly Auth0BrowserApiFactory _factory;

	public Auth0BrowserAuthenticationIntegrationTests(Auth0BrowserApiFactory factory) => _factory = factory;

	[Theory]
	[InlineData("https://evil.example/", "/")]
	[InlineData("//evil.example/", "/")]
	[InlineData("/\\evil.example/", "/")]
	[InlineData("/auth/login", "/")]
	[InlineData("/#history", "/#history")]
	public async Task LoginOnlyReturnsToLocalConsole(string requested, string expected)
	{
		using var client = _factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
		var response = await client.GetAsync("/auth/login?returnUrl=" + Uri.EscapeDataString(requested));
		var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
		var options = _factory.Services.GetRequiredService<IOptionsMonitor<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>>().Get("OpenIdConnect");
		Assert.Equal(expected, options.StateDataFormat.Unprotect(query["state"]!)!.RedirectUri);
	}

	[Fact]
	public async Task BrowserCookieGetsCsrfTokenAndCannotMutateWithoutIt()
	{
		using var client = _factory.CreateCookieClient();
		using var session = JsonDocument.Parse(await client.GetStringAsync("/auth/session"));
		Assert.True(session.RootElement.GetProperty("canAccess").GetBoolean());
		var token = session.RootElement.GetProperty("csrfToken").GetString();
		Assert.False(string.IsNullOrWhiteSpace(token));
		Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/auth/logout", null)).StatusCode);
		client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
		var logout = await client.PostAsync("/auth/logout", null);
		Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
		Assert.Contains(logout.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("__Host-IncidentResponseSession=;", StringComparison.Ordinal));
		Assert.Equal("https://localhost/", QueryHelpers.ParseQuery(logout.Headers.Location!.Query)["post_logout_redirect_uri"]);
	}

	[Fact]
	public async Task ExpiredCookieCannotReadData()
	{
		using var client = _factory.CreateCookieClient(expired: true);
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/projects")).StatusCode);
	}

	[Fact]
	public async Task AccountWithoutResponderAccessCanStillSignOut()
	{
		using var client = _factory.CreateCookieClient(responder: false);
		using var session = JsonDocument.Parse(await client.GetStringAsync("/auth/session"));
		Assert.False(session.RootElement.GetProperty("canAccess").GetBoolean());
		client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.RootElement.GetProperty("csrfToken").GetString());
		Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/auth/logout", null)).StatusCode);
	}

	[Fact]
	public async Task FailedCallbackReturnsSafeLoginPage()
	{
		using var client = _factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
		var response = await client.GetAsync("/signin-oidc?error=access_denied&error_description=private-provider-details");
		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.StartsWith("/login?reason=", response.Headers.Location!.ToString());
		Assert.DoesNotContain("private-provider-details", response.Headers.Location.ToString());
	}

	[Fact]
	public async Task LoginRedirectRequestsConfiguredApiAudienceAndPkce()
	{
		using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

		var response = await client.GetAsync("/auth/login?returnUrl=%2F%23config");
		var location = response.Headers.Location?.ToString() ?? string.Empty;
		var body = await response.Content.ReadAsStringAsync();
		var query = QueryHelpers.ParseQuery(new Uri(location).Query);

		Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"Expected redirect but received {(int)response.StatusCode}: {body}");
		Assert.Equal("https://incident-response-agent-api", query["audience"]);
		Assert.Contains("code_challenge=", location, StringComparison.OrdinalIgnoreCase);
		Assert.Equal("openid profile email", query["scope"]);
	}
}

public sealed class Auth0BrowserApiFactory : WebApplicationFactory<Program>
{
	public HttpClient CreateCookieClient(bool expired = false, bool responder = true)
	{
		var client = CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
		var options = Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
		// OIDC identities may retain this authentication type inside a valid cookie.
		var identity = new ClaimsIdentity([new Claim("name", "browser-responder"), new Claim("https://incidentresponseagent/roles", responder ? "responder" : "viewer")], "AuthenticationTypes.Federation", "name", "https://incidentresponseagent/roles");
		var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow.AddHours(-1), ExpiresUtc = expired ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow.AddMinutes(5) }, CookieAuthenticationDefaults.AuthenticationScheme);
		client.DefaultRequestHeaders.Add("Cookie", "__Host-IncidentResponseSession=" + options.TicketDataFormat.Protect(ticket));
		return client;
	}
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("AuthenticationTests")
			.UseSetting("Authentication:AllowDevelopmentIdentity", "false")
			.UseSetting("Authentication:Authority", "https://incident-response-test.us.auth0.com/")
			.UseSetting("Authentication:Audience", "https://incident-response-agent-api")
			.UseSetting("Authentication:BrowserClientId", "test-browser-client")
			.UseSetting("Authentication:BrowserClientSecret", "test-browser-secret")
			.UseSetting("Authentication:BrowserScope", "openid profile email")
			.UseSetting("Authentication:RoleClaimType", "https://incidentresponseagent/roles")
			.UseSetting("Authentication:RequiredScope", "incident_response");
		builder.ConfigureTestServices(services =>
		{
			services.AddDataProtection().UseEphemeralDataProtectionProvider();
			services.PostConfigure<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>(Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectDefaults.AuthenticationScheme, options =>
			{
				options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration
				{
					Issuer = "https://incident-response-test.us.auth0.com/",
					AuthorizationEndpoint = "https://incident-response-test.us.auth0.com/authorize",
					TokenEndpoint = "https://incident-response-test.us.auth0.com/oauth/token",
					EndSessionEndpoint = "https://incident-response-test.us.auth0.com/oidc/logout"
				});
			});
		});
	}
}

public sealed class AuthenticationApiFactory : WebApplicationFactory<Program>
{
	private const string Issuer = "https://issuer.incident-response.test";
	private const string Audience = "incident-response-api";
	private readonly RSA _rsa = RSA.Create(2048);
	private readonly RsaSecurityKey _signingKey;

	public AuthenticationApiFactory() => _signingKey = new RsaSecurityKey(_rsa) { KeyId = "integration-test-key" };

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("AuthenticationTests")
			.UseSetting("Storage:Incidents:IncidentRecordsPath", Path.Combine(Path.GetTempPath(), "ira-auth-tests", Guid.NewGuid().ToString("N"), "incidents.json"))
			.UseSetting("Authentication:AllowDevelopmentIdentity", "false")
			.UseSetting("Authentication:Authority", Issuer)
			.UseSetting("Authentication:Audience", Audience)
			.UseSetting("Authentication:RoleClaimType", "https://incidentresponseagent/roles")
			.UseSetting("Authentication:RequiredScope", "incident.respond");
		builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
		{
			options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration
			{
				Issuer = Issuer,
				SigningKeys = { _signingKey }
			});
		}));
	}

	public HttpClient CreateAuthenticatedClient(params Claim[] claims)
	{
		var client = CreateClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(claims));
		return client;
	}

	private string CreateToken(IEnumerable<Claim> claims)
	{
		var token = new JwtSecurityToken(Issuer, Audience, claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5), new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256));
		return new JwtSecurityTokenHandler().WriteToken(token);
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		if (disposing) _rsa.Dispose();
	}
}
