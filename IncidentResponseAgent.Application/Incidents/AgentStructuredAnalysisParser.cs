using System.Text.Json;

namespace IncidentResponseAgent.Application.Incidents;

public static class AgentStructuredAnalysisParser
{
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	public static AgentStructuredAnalysis? TryParse(string analysisText)
	{
		if (string.IsNullOrWhiteSpace(analysisText))
		{
			return null;
		}

		var json = analysisText.Trim();
		if (json is null)
		{
			return null;
		}

		try
		{
			var result = JsonSerializer.Deserialize<AgentStructuredAnalysis>(json, SerializerOptions);
			return result?.Severity is "SEV-1" or "SEV-2" or "SEV-3" or "SEV-4" or "SEV-5" ? result : null;
		}
		catch (JsonException)
		{
			return null;
		}
	}

}
