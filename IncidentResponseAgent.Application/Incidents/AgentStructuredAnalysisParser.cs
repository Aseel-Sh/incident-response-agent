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

		var json = ExtractJsonObject(analysisText);
		if (json is null)
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<AgentStructuredAnalysis>(json, SerializerOptions);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static string? ExtractJsonObject(string text)
	{
		var start = text.IndexOf('{');
		var end = text.LastIndexOf('}');
		if (start < 0 || end <= start)
		{
			return null;
		}

		return text[start..(end + 1)];
	}
}
