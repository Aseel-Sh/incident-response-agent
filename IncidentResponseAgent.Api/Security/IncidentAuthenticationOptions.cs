namespace IncidentResponseAgent.Api.Security;

public sealed record IncidentAuthenticationOptions
{
	public const string Scheme = "IncidentApiKey";
	public bool AllowDevelopmentIdentity { get; init; }
	public string DevelopmentIdentity { get; init; } = "local-operator";
	public IReadOnlyList<IncidentApiUser> Users { get; init; } = Array.Empty<IncidentApiUser>();
}

public sealed record IncidentApiUser
{
	public required string Name { get; init; }
	public required string ApiKey { get; init; }
	public string Role { get; init; } = "responder";
}
