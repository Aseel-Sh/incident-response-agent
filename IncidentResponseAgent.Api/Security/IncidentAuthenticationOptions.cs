namespace IncidentResponseAgent.Api.Security;

public sealed record IncidentAuthenticationOptions
{
	public const string DevelopmentScheme = "IncidentDevelopment";
	public string Authority { get; init; } = string.Empty;
	public string Audience { get; init; } = string.Empty;
	public string BrowserClientId { get; init; } = string.Empty;
	public string? BrowserClientSecret { get; init; }
	public string BrowserScope { get; init; } = "openid profile email";
	public string NameClaimType { get; init; } = "name";
	public string RoleClaimType { get; init; } = "https://incidentresponseagent/roles";
	public string? RequiredScope { get; init; }
	public bool AllowDevelopmentIdentity { get; init; }
	public string DevelopmentIdentity { get; init; } = "local-operator";
}
