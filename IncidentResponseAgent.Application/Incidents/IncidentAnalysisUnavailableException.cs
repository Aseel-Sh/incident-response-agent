namespace IncidentResponseAgent.Application.Incidents;

public sealed class IncidentAnalysisUnavailableException(string message) : Exception(message);
