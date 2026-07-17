using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IncidentResponseAgent.Api.Security;

public sealed class IncidentDevelopmentAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
	IOptions<IncidentAuthenticationOptions> incidentOptions,
	ILoggerFactory logger,
	UrlEncoder encoder)
	: AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var configured = incidentOptions.Value;
		if (!configured.AllowDevelopmentIdentity) return Task.FromResult(AuthenticateResult.NoResult());
		var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, configured.DevelopmentIdentity), new Claim(ClaimTypes.Role, "admin")], IncidentAuthenticationOptions.DevelopmentScheme);
		return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), IncidentAuthenticationOptions.DevelopmentScheme)));
	}
}
