export function incidentAnalytics(incidents) {
  const severity = new Map();
  const status = new Map();
  const days = new Map();
  let missingTimestamp = 0;
  for (const item of incidents) {
    const sev = String(item.severity || "unknown").toLowerCase().replace("-", "");
    severity.set(sev, (severity.get(sev) || 0) + 1);
    const lifecycle = item.status || "unknown";
    status.set(lifecycle, (status.get(lifecycle) || 0) + 1);
    const timestamp = Date.parse(item.createdAtUtc);
    if (!Number.isFinite(timestamp)) { missingTimestamp++; continue; }
    const day = new Date(timestamp).toISOString().slice(0, 10);
    days.set(day, (days.get(day) || 0) + 1);
  }
  return { severity: [...severity].sort(), status: [...status].sort(), days: [...days].sort(), missingTimestamp };
}
