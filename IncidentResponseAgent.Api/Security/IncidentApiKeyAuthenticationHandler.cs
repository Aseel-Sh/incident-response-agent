using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Api.Security;

public sealed class IncidentApiKeyAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
	IOptions<IncidentAuthenticationOptions> incidentOptions,
	ILoggerFactory logger,
	UrlEncoder encoder)
	: AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var configured = incidentOptions.Value;
		IncidentApiUser? user = null;
		if (Request.Headers.TryGetValue("X-IRA-API-Key", out var supplied))
			user = configured.Users.FirstOrDefault(item => CryptographicEquals(item.ApiKey, supplied.ToString()));
		if (user is null && configured.AllowDevelopmentIdentity)
			user = new IncidentApiUser { Name = configured.DevelopmentIdentity, ApiKey = "development", Role = "admin" };
		if (user is null) return Task.FromResult(AuthenticateResult.NoResult());
		var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, user.Name), new Claim(ClaimTypes.Role, user.Role)], IncidentAuthenticationOptions.Scheme);
		return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), IncidentAuthenticationOptions.Scheme)));
	}

	private static bool CryptographicEquals(string expected, string actual)
	{
		if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual)) return false;
		var left = System.Text.Encoding.UTF8.GetBytes(expected);
		var right = System.Text.Encoding.UTF8.GetBytes(actual);
		return left.Length == right.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(left, right);
	}
}
