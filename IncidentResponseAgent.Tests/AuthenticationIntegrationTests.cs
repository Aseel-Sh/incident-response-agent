using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace IncidentResponseAgent.Tests;

public sealed class AuthenticationIntegrationTests : IClassFixture<AuthenticationApiFactory>
{
	private readonly AuthenticationApiFactory _factory;

	public AuthenticationIntegrationTests(AuthenticationApiFactory factory) => _factory = factory;

	[Fact]
	public async Task InvalidBearerTokenIsRejected()
	{
		using var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

		var response = await client.GetAsync("/api/identity/me");

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task ValidResponderRoleCanUseProtectedApi()
	{
		using var client = _factory.CreateAuthenticatedClient(new Claim("name", "response-lead"), new Claim("roles", "responder"));

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
		using var client = _factory.CreateAuthenticatedClient(new Claim("name", "response-lead"), new Claim("roles", "responder"));

		var response = await client.DeleteAsync("/api/projects/project-that-does-not-exist");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task AdminRoleCanReachAdminApi()
	{
		using var client = _factory.CreateAuthenticatedClient(new Claim("name", "platform-admin"), new Claim("roles", "admin"));

		var response = await client.DeleteAsync("/api/projects/project-that-does-not-exist");

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
			.UseSetting("Authentication:AllowDevelopmentIdentity", "false")
			.UseSetting("Authentication:Authority", Issuer)
			.UseSetting("Authentication:Audience", Audience)
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
