export function diagnosticText(value, fallback = "Not available") {
  if (typeof value !== "string") return fallback;
  const text = value.trim();
  return !text || ["?", "undefined", "null", "unknown", "[object object]"].includes(text.toLowerCase()) ? fallback : text;
}
export function diagnosticRows(provider = {}) {
  const rows = [];
  for (const [label, key] of [["Evidence gathering", "evidenceGatheringDurationMilliseconds"], ["RAG retrieval", "ragDurationMilliseconds"], ["Model execution", "modelDurationMilliseconds"]]) {
    const value = provider[key];
    if (typeof value === "number" && Number.isFinite(value) && value >= 0) rows.push([label, `${value.toLocaleString()} ms`]);
  }
  const stages = { before_model_execution: "Before model execution", during_model_execution: "During model execution", after_model_response_validation: "Response validation" };
  if (provider.fallbackStage) rows.push(["Fallback stage", stages[provider.fallbackStage] || "Not available"]);
  if (provider.timeoutSource) rows.push(["Timeout source", /CancelAfter/.test(provider.timeoutSource) ? "Application model time limit" : diagnosticText(provider.timeoutSource)]);
  return rows;
}

export function vectorStoreLocation(result) {
  const provider = diagnosticText(result.vectorStoreProvider).toLowerCase();
  if (provider.startsWith("qdrant") && !provider.includes("/sqlite")) {
    return [diagnosticText(result.vectorStoreEndpoint, "Endpoint not reported"), diagnosticText(result.vectorStoreCollection, "Collection not reported")].join(" · ");
  }
  return diagnosticText(result.databasePath, "Path not reported");
}
