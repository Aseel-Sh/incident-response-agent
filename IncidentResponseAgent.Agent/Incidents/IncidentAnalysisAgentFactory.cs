namespace IncidentResponseAgent.Agent.Incidents;

public sealed class IncidentAnalysisAgentFactory : IIncidentAnalysisAgentFactory
{
	private const string ProviderEnvironmentVariable = "IRA_AGENT_PROVIDER";
	private const string ModelEnvironmentVariable = "IRA_AGENT_MODEL";
	private const string EndpointEnvironmentVariable = "IRA_AGENT_ENDPOINT";
	private const string ApiKeyEnvironmentVariable = "IRA_AGENT_API_KEY";

	public IncidentAnalysisAgentProfile Create()
	{
		var options = new IncidentAnalysisAgentOptions
		{
			Provider = GetValue(ProviderEnvironmentVariable, "unset"),
			Model = GetValue(ModelEnvironmentVariable, "unset"),
			Endpoint = GetValue(EndpointEnvironmentVariable),
			ApiKey = GetValue(ApiKeyEnvironmentVariable)
		};

		return new IncidentAnalysisAgentProfile
		{
			Name = "IncidentAnalysisAgent",
			Provider = options.Provider,
			Model = options.Model,
			Endpoint = options.Endpoint,
			ApiKey = options.ApiKey
		};
	}

	private static string? GetValue(string key)
	{
		var value = Environment.GetEnvironmentVariable(key);
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}

	private static string GetValue(string key, string fallback)
	{
		return GetValue(key) ?? fallback;
	}
}