export function escapeHtml(value) { return String(value ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#039;"); }

export function formatTime(value) { return value ? new Date(value).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }) : ""; }

export function formatTimestamp(value) { return value ? new Date(value).toLocaleString() : ""; }

export function formatReadableTime(value) {
  if (!value) return "";
  return new Date(value).toLocaleTimeString([], { hour: "numeric", minute: "2-digit", second: "2-digit" });
}

export function formatEvidenceTime(details) {
  const match = String(details || "").match(/\d{4}-\d{2}-\d{2}T[^\s]+/);
  return match ? formatReadableTime(match[0]) : "";
}

export function formatAgo(value) {
  if (!value) return "not run";
  const timestamp = new Date(value).getTime();
  if (!Number.isFinite(timestamp)) return "not run";
  const minutes = Math.max(0, Math.round((Date.now() - timestamp) / 60000));
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 30) return `${days}d ago`;
  const months = Math.floor(days / 30);
  if (months < 12) return `${months}mo ago`;
  return `${Math.floor(days / 365)}y ago`;
}

export function shortenId(value) { const text = String(value || ""); return text.length > 12 ? `${text.slice(0, 8)}...` : text || "none"; }

export function formatIncidentTitle(title) { return String(title || "").replace("request error rate threshold breached", "error rate threshold").replace("queue depth threshold breached", "queue depth threshold").replace("suspicious log pattern", "log signal"); }

export function formatEvidenceSource(source) { return String(source || "evidence").replace("tool.logs", "LOG").replace("tool.metrics", "METRIC").replace("incident.description", "INCIDENT").replace(/^rag\.runbook\./, "RUNBOOK ").replace(/^history\.incident\./, "HISTORY "); }

export function evidenceKind(source) { return String(source || "").includes("metrics") ? "metric" : String(source || "").includes("logs") ? "log" : "generic"; }

export function formatNotes(value) { return String(value || "No notes captured.").trim(); }

export function normalizeAction(value) { return String(value || "").toLowerCase().replace(/[^a-z0-9]+/g, " ").trim(); }

export function shortRunbookName(value) { const text = String(value || "runbook/checkout-db.md").replaceAll("\\", "/"); const match = text.match(/KnowledgeBase\/(.+)$/i); return match ? `runbook/${match[1]}` : text.split("/").slice(-2).join("/"); }

export function parseJson(value) { try { return JSON.parse(value); } catch { return null; } }

export function similarPercent(score, index) { return Math.round((Number(score) || 0) * 100); }

export function scoreColor(percent) { return percent >= 90 ? "green" : percent >= 75 ? "yellow" : "red"; }

export function formatHistoryId(value, index) { const text = String(value || ""); return text ? `INC-${text.replace(/-/g, "").slice(0, 8).toUpperCase()}` : "ID not reported"; }

export function formatSeverityLabel(value) { const key = String(value || "").toLowerCase().replace("-", ""); return ({ sev1: "SEV-1", sev2: "SEV-2", sev3: "SEV-3", sev4: "SEV-4", sev5: "SEV-5" })[key] || "Unknown"; }

export function formatStatusLabel(value) { return ({ candidate: "Candidate", false_positive: "False positive", ignored: "Ignored", merged: "Merged", recovered: "Recovered", new: "Confirmed", active: "Active", mitigated: "Mitigated", resolved: "Resolved" })[String(value || "").toLowerCase()] || "Unknown"; }

export function normalizeIncidentStatus(value) {
  const status = String(value || "active").toLowerCase();
  if (status === "ack") return "active";
  return ["new", "active", "mitigated", "resolved", "recovered"].includes(status) ? status : "unknown";
}

export function inferHistoryTags(value) {
  const text = String(value || "").toLowerCase();
  const tags = [];
  if (text.includes("latency") || text.includes("p95")) tags.push("latency");
  if (text.includes("checkout")) tags.push("checkout");
  if (text.includes("database") || text.includes("db") || text.includes("pool")) tags.push("database");
  if (text.includes("auth") || text.includes("token")) tags.push("auth");
  if (text.includes("cdn") || text.includes("cache")) tags.push("cache");
  return tags.length ? tags.slice(0, 3) : ["incident"];
}

export function normalizeArray(value) { return Array.isArray(value) ? value : Array.isArray(value?.value) ? value.value : []; }

export function cleanStatusMessage(value) {
  const sentences = String(value || "").split(/(?<=[.!?])\s+/).map((item) => item.trim()).filter(Boolean);
  return [...new Map(sentences.map((item) => [item.toLowerCase(), item])).values()].join(" ");
}

export function normalizeConfidence(value) {
  const text = String(value || "Unknown").toLowerCase();
  if (text.includes("high")) return "High";
  if (text.includes("low")) return "Low";
  return text.includes("medium") ? "Medium" : "Unknown";
}

export function splitTags(value) { return String(value || "").split(",").map((tag) => tag.trim()).filter(Boolean); }

export function emptyToNull(value) { const text = String(value || "").trim(); return text ? text : null; }
