using IncidentResponseAgent.Application.Incidents;

namespace IncidentResponseAgent.Tests;

public sealed class AgentStructuredAnalysisParserTests
{
	[Fact]
	public void TryParseMapsStructuredJson()
	{
		const string json = """
{
  "summary": "Checkout has elevated 5xx responses.",
  "evidence": [
    {
      "summary": "Runbook match found.",
      "source": "rag.runbook.checkout-1",
      "details": "Checkout 5xx Triage"
    }
  ],
  "hypotheses": [
    {
      "description": "Recent checkout regression.",
      "inferenceStrength": "Medium",
      "confidence": "Medium",
      "supportingEvidence": ["RAG matched checkout triage."],
      "evidenceReferences": ["rag.runbook.checkout-1"]
    }
  ],
  "recommendedActions": [
    {
      "description": "Confirm blast radius.",
      "priority": "High",
      "rationale": "Scope drives mitigation.",
      "supportingSignals": ["tool.logs"]
    }
  ],
  "confidence": "Medium",
  "notes": "Use tool-backed evidence only."
}
""";

		var result = AgentStructuredAnalysisParser.TryParse(json);

		Assert.NotNull(result);
		Assert.Equal("Checkout has elevated 5xx responses.", result.Summary);
		Assert.Single(result.Evidence);
		Assert.Single(result.Hypotheses);
		Assert.Single(result.RecommendedActions);
		Assert.Equal("Medium", result.Confidence);
	}
}
