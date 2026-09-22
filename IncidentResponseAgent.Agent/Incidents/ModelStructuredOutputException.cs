namespace IncidentResponseAgent.Agent.Incidents;

public sealed class ModelStructuredOutputException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
