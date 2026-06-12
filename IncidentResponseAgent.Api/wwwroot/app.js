const healthStatus = requireElement("#healthStatus");
const incidentForm = requireElement("#incidentForm");
const ragForm = requireElement("#ragForm");
const analysisOutput = requireElement("#analysisOutput");
const analysisStatus = requireElement("#analysisStatus");
const ragSummary = requireElement("#ragSummary");
const ragResults = requireElement("#ragResults");
const recentOutput = requireElement("#recentOutput");
const detectedOutput = requireElement("#detectedOutput");
const sourcesOutput = requireElement("#sourcesOutput");
const lastScan = requireElement("#lastScan");
const analyzeButton = requireElement("#analyzeButton");
const scanButton = requireElement("#scanButton");
const themeToggle = requireElement("#themeToggle");
const incidentSearch = requireElement("#incidentSearch");
const incidentSort = requireElement("#incidentSort");
const workspaceTitle = requireElement("#workspaceTitle");
const workspaceDescription = requireElement("#workspaceDescription");
const workspaceEyebrow = requireElement("#workspaceEyebrow");
const evaluationOutput = requireElement("#evaluationOutput");
const toastRegion = requireElement("#toastRegion");
const scanFeedback = requireElement("#scanFeedback");

const tabCopy = {
  monitor: ["Live Triage", "Monitor", "Detected candidates from logs and metrics, ready for one-click analysis."],
  analyze: ["Agent Workspace", "Analyze", "Manual or detected incidents flow through evidence, RAG, model reasoning, and fallback metadata."],
  runbooks: ["Knowledge Base", "Runbooks", "Matched chunks, scores, source files, embeddings, and vector store diagnostics."],
  history: ["Investigation Trail", "History", "Recent analyses with sessions, turns, confidence, notes, and provider metadata."],
  sources: ["Connectivity", "Sources", "Runtime paths for logs, metrics, runbooks, vector storage, sessions, and signal ingestion."],
  evaluation: ["Quality Gate", "Evaluation", "Rubric scenarios for evidence, hypotheses, recommendations, and expected operational signals."]
};

const severityRank = { Critical: 4, High: 3, Medium: 2, Low: 1, Unknown: 0 };
const sourceIcons = {
  Logs: "terminal",
  Metrics: "chart",
  Runbooks: "book",
  "Vector Search": "database",
  "Runbook Vector Cache": "database",
  "Investigation Sessions": "history",
  "Incident History": "file"
};
const iconPaths = {
  activity: '<path d="M22 12h-4l-3 8L9 4l-3 8H2"/>',
  book: '<path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M4 4.5A2.5 2.5 0 0 1 6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5z"/>',
  brain: '<path d="M9.5 2A2.5 2.5 0 0 0 7 4.5v.2A3.5 3.5 0 0 0 4 8v1a3 3 0 0 0 0 6v1a3.5 3.5 0 0 0 3 3.3v.2A2.5 2.5 0 0 0 9.5 22H12V2Z"/><path d="M14.5 2A2.5 2.5 0 0 1 17 4.5v.2A3.5 3.5 0 0 1 20 8v1a3 3 0 0 1 0 6v1a3.5 3.5 0 0 1-3 3.3v.2a2.5 2.5 0 0 1-2.5 2.5H12V2Z"/>',
  chart: '<path d="M3 3v18h18"/><path d="M7 14l4-4 3 3 5-6"/>',
  check: '<path d="M20 6 9 17l-5-5"/>',
  database: '<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v14c0 1.7 3.6 3 8 3s8-1.3 8-3V5"/><path d="M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3"/>',
  file: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z"/><path d="M14 2v6h6"/>',
  history: '<path d="M3 12a9 9 0 1 0 3-6.7"/><path d="M3 3v6h6"/><path d="M12 7v5l3 2"/>',
  moon: '<path d="M21 12.8A8.5 8.5 0 1 1 11.2 3a6.7 6.7 0 0 0 9.8 9.8Z"/>',
  play: '<path d="m8 5 11 7-11 7Z"/>',
  plug: '<path d="M12 22v-5"/><path d="M9 8V2"/><path d="M15 8V2"/><path d="M6 8h12v4a6 6 0 0 1-12 0Z"/>',
  radar: '<path d="M19.1 4.9A10 10 0 1 1 4.9 19.1"/><path d="M12 12 21 3"/><circle cx="12" cy="12" r="2"/><path d="M16.2 7.8a6 6 0 1 1-8.4 8.4"/>',
  refresh: '<path d="M21 12a9 9 0 0 1-15.5 6.2L3 16"/><path d="M3 21v-5h5"/><path d="M3 12A9 9 0 0 1 18.5 5.8L21 8"/><path d="M21 3v5h-5"/>',
  search: '<circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>',
  server: '<rect x="3" y="4" width="18" height="8" rx="2"/><rect x="3" y="12" width="18" height="8" rx="2"/><path d="M7 8h.01M7 16h.01"/>',
  sparkles: '<path d="M12 3l1.7 5.1L19 10l-5.3 1.9L12 17l-1.7-5.1L5 10l5.3-1.9Z"/><path d="M5 3v4M3 5h4M19 17v4M17 19h4"/>',
  terminal: '<path d="m4 17 6-6-6-6"/><path d="M12 19h8"/>',
  wand: '<path d="M15 4V2"/><path d="M15 16v-2"/><path d="M8 9H6"/><path d="M20 9h-2"/><path d="m17.8 6.2 1.4-1.4"/><path d="m10.8 13.2-1.4 1.4"/><path d="m10.8 4.8-1.4-1.4"/><path d="m17.8 11.8 1.4 1.4"/><path d="m3 21 9-9"/>'
};
const loadingSteps = [
  "The API is gathering evidence and waiting for the agent response."
];

let detectedCandidates = [];
let sourceRows = [];
let loadingTimer = null;

document.querySelectorAll(".nav-tab").forEach((button) => {
  button.addEventListener("click", () => activateTab(button.dataset.tab));
});

themeToggle.addEventListener("click", toggleTheme);
requireElement("#sampleButton").addEventListener("click", loadSampleIncident);
requireElement("#recentButton").addEventListener("click", loadRecent);
scanButton.addEventListener("click", () => loadDetected({ userInitiated: true }));
incidentSearch.addEventListener("input", renderDetectedCandidates);
incidentSort.addEventListener("change", renderDetectedCandidates);

detectedOutput.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-action]");
  if (!button) {
    return;
  }

  const candidate = detectedCandidates.find((item) => item.id === button.dataset.id);
  if (!candidate) {
    return;
  }

  fillIncidentForm(candidate);
  activateTab("analyze");

  if (button.dataset.action === "analyze") {
    await analyzeCurrentIncident();
  }
});

incidentForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  await analyzeCurrentIncident();
});

ragForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  await searchRag();
});

function loadSampleIncident() {
  fillIncidentForm({
    title: "Checkout 5xx spike",
    description: "Customers are seeing intermittent 500 responses during checkout. Error rate increased after the latest deployment.",
    severity: "High",
    serviceName: "checkout-api",
    environment: "production",
    suggestedTags: ["checkout", "5xx", "latency"]
  });
  activateTab("analyze");
}

function activateTab(tabName) {
  document.querySelectorAll(".nav-tab").forEach((button) => {
    button.classList.toggle("active", button.dataset.tab === tabName);
  });

  document.querySelectorAll(".tab-view").forEach((view) => {
    view.classList.toggle("active", view.id === `${tabName}View`);
  });

  const [eyebrow, title, description] = tabCopy[tabName] || tabCopy.monitor;
  workspaceEyebrow.textContent = eyebrow;
  workspaceTitle.textContent = title;
  workspaceDescription.textContent = description;
}

function toggleTheme() {
  const nextTheme = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
  applyTheme(nextTheme);
  setStoredTheme(nextTheme);
}

function applyTheme(theme) {
  document.documentElement.dataset.theme = theme;
  themeToggle.dataset.icon = theme === "dark" ? "sparkles" : "moon";
  themeToggle.innerHTML = "";
  themeToggle.dataset.iconHydrated = "";
  hydrateIcons(themeToggle);
}

async function checkHealth() {
  try {
    const result = await requestJson("/health");
    healthStatus.textContent = result.status;
    healthStatus.classList.add("ok");
  } catch {
    healthStatus.textContent = "API unavailable";
    healthStatus.classList.add("error");
  }
}

async function loadDetected({ userInitiated = false } = {}) {
  if (userInitiated) {
    scanButton.disabled = true;
    scanButton.textContent = "Scanning...";
    detectedOutput.innerHTML = `<div class="empty-state">Scanning connected logs and metrics...</div>`;
    scanFeedback.innerHTML = `<span class="status-pill status-pending">Scanning</span><span>Checking active log and metric sources now.</span>`;
  }

  try {
    detectedCandidates = normalizeArray(await requestJson("/api/incidents/detected"));
    lastScan.textContent = `Last scan ${new Date().toLocaleTimeString()}`;
    updateAnalytics();
    renderDetectedCandidates();
    if (userInitiated) {
      const count = detectedCandidates.length;
      scanFeedback.innerHTML = `<span class="status-pill status-connected">Scan complete</span><span>${count} candidate${count === 1 ? "" : "s"} found from connected sources.</span>`;
      showToast("Scan complete", `${count} candidate${count === 1 ? "" : "s"} found.`, "success");
    }
  } catch (error) {
    renderError(detectedOutput, error);
    if (userInitiated) {
      scanFeedback.innerHTML = `<span class="status-pill status-missing">Scan failed</span><span>${escapeHtml(error.message || String(error))}</span>`;
      showToast("Scan failed", error.message || String(error), "error");
    }
  } finally {
    if (userInitiated) {
      scanButton.disabled = false;
      scanButton.innerHTML = `${iconSvg("refresh")}<span>Scan Now</span>`;
    }
  }
}

async function loadSources() {
  try {
    sourceRows = normalizeArray(await requestJson("/api/operations/sources"));
    sourcesOutput.innerHTML = sourceRows.map(renderSource).join("");
    hydrateIcons(sourcesOutput);
  } catch (error) {
    renderError(sourcesOutput, error);
  }
}

async function loadEvaluation() {
  evaluationOutput.innerHTML = `<div class="empty-state">Loading evaluation scenarios...</div>`;
  try {
    const scenarios = normalizeArray(await requestJson("/api/evaluation/scenarios"));
    evaluationOutput.innerHTML = scenarios.map(renderEvaluationScenario).join("") || `<div class="empty-state">No evaluation scenarios configured.</div>`;
    hydrateIcons(evaluationOutput);
  } catch (error) {
    renderError(evaluationOutput, error);
  }
}

function updateAnalytics() {
  document.querySelector("#totalDetected").textContent = detectedCandidates.length;
  document.querySelector("#criticalDetected").textContent = detectedCandidates.filter((item) => item.severity === "Critical").length;
  document.querySelector("#logDetected").textContent = detectedCandidates.filter((item) => String(item.source || "").includes("logs")).length;
  document.querySelector("#metricDetected").textContent = detectedCandidates.filter((item) => String(item.source || "").includes("metrics")).length;
}

function renderDetectedCandidates() {
  const query = incidentSearch.value.trim().toLowerCase();
  const sorted = [...detectedCandidates]
    .filter((item) => matchesIncidentQuery(item, query))
    .sort(compareDetectedCandidates);

  detectedOutput.innerHTML = `
    <div class="table-head">
      <span>Severity</span>
      <span>Incident</span>
      <span>Source</span>
      <span>Signals</span>
      <span>Actions</span>
    </div>
    ${sorted.map(renderDetectedCandidate).join("") || `<div class="empty-state table-empty">No candidates match the current filter.</div>`}
  `;
  hydrateIcons(detectedOutput);
}

function matchesIncidentQuery(item, query) {
  if (!query) {
    return true;
  }

  return [
    item.title,
    item.description,
    item.serviceName,
    item.environment,
    item.source,
    ...(item.signals || []),
    ...(item.suggestedTags || [])
  ].filter(Boolean).join(" ").toLowerCase().includes(query);
}

function compareDetectedCandidates(left, right) {
  switch (incidentSort.value) {
    case "time":
      return new Date(right.detectedAtUtc) - new Date(left.detectedAtUtc);
    case "source":
      return String(left.source).localeCompare(String(right.source));
    case "service":
      return String(left.serviceName || "").localeCompare(String(right.serviceName || ""));
    case "severity":
    default:
      return (severityRank[right.severity] || 0) - (severityRank[left.severity] || 0)
        || new Date(right.detectedAtUtc) - new Date(left.detectedAtUtc);
  }
}

async function analyzeCurrentIncident() {
  setAnalyzing(true);
  activateTab("analyze");
  renderLoadingAnalysis();

  const form = new FormData(incidentForm);
  const payload = {
    title: form.get("title"),
    description: form.get("description"),
    severity: form.get("severity"),
    serviceName: emptyToNull(form.get("serviceName")),
    environment: emptyToNull(form.get("environment")),
    sessionId: emptyToNull(form.get("sessionId")),
    tags: splitTags(form.get("tags"))
  };

  try {
    const result = await requestJson("/api/incidents/analyze", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    incidentForm.sessionId.value = result.sessionId;
    renderAnalysis(result);
    await Promise.all([loadRecent(), searchRag()]);
    showToast("Analysis complete", `Session ${result.sessionId}, turn ${result.sessionTurnNumber}.`, result.usedFallbackAnalysis ? "warning" : "success");
  } catch (error) {
    renderError(analysisOutput, error);
    analysisStatus.textContent = "Analysis failed. Check the error below.";
    showToast("Analysis failed", error.message || String(error), "error");
  } finally {
    setAnalyzing(false);
  }
}

async function searchRag() {
  ragResults.innerHTML = `<div class="empty-state">Searching runbooks...</div>`;
  ragSummary.textContent = "";

  const form = new FormData(ragForm);
  const incidentFormData = new FormData(incidentForm);
  const query = encodeURIComponent(form.get("query") || "");
  const serviceName = encodeURIComponent(incidentFormData.get("serviceName") || "");
  const environment = encodeURIComponent(incidentFormData.get("environment") || "");

  try {
    const result = await requestJson(`/api/runbooks/search?query=${query}&serviceName=${serviceName}&environment=${environment}&maxResults=5`);
    ragSummary.innerHTML = `
      <span>Matches: <strong>${result.matches.length}</strong></span>
      <span>Knowledge base: <strong>runbooks</strong></span>
    `;
    ragResults.innerHTML = result.matches.map(renderRagMatch).join("") || `<div class="empty-state">No matches.</div>`;
    hydrateIcons(ragResults);
  } catch (error) {
    renderError(ragResults, error);
  }
}

async function loadRecent() {
  recentOutput.innerHTML = `<div class="empty-state">Loading recent analyses...</div>`;
  try {
    const results = await requestJson("/api/incidents/recent?maxResults=8");
    recentOutput.innerHTML = results.map(renderHistoryItem).join("") || `<div class="empty-state">No saved analyses yet.</div>`;
    hydrateIcons(recentOutput);
  } catch (error) {
    renderError(recentOutput, error);
  }
}

function renderHistoryItem(item) {
  const providerMode = item.usedFallbackAnalysis
    ? `<span class="status-pill status-warning">fallback</span>`
    : `<span class="status-pill status-connected">model</span>`;
  const confidence = item.confidence || "unknown";

  return `
    <article class="history-card">
      <div class="history-card-main">
        <div>
          <h4>${escapeHtml(item.incidentSummary)}</h4>
          <p>${escapeHtml(formatNotes(item.notes))}</p>
          <div class="badge-row">
            <span class="badge">Session ${escapeHtml(shortenId(item.sessionId))}</span>
            <span class="badge">Turn ${item.sessionTurnNumber}</span>
            <span class="badge">${escapeHtml(formatAnalysisProvider(item))}</span>
          </div>
        </div>
      </div>
      <div class="history-aside">
        ${providerMode}
        <span class="confidence-inline">${escapeHtml(confidence)} confidence</span>
      </div>
    </article>
  `;
}

function renderDetectedCandidate(item) {
  const sourceText = formatSourceName(item.source);
  return `
    <article class="table-row">
      <div><span class="severity severity-${escapeHtml(String(item.severity).toLowerCase())}">${escapeHtml(item.severity)}</span></div>
      <div>
        <h4>${escapeHtml(formatIncidentTitle(item.title))}</h4>
        <p>${escapeHtml(item.serviceName || "unknown service")} / ${escapeHtml(item.environment || "unknown environment")}</p>
        <p class="meta">${formatDate(item.detectedAtUtc)}</p>
      </div>
      <div><span class="badge">${escapeHtml(sourceText)}</span></div>
      <div class="signal-list">${(item.signals || []).slice(0, 3).map((value) => `<span>${escapeHtml(value)}</span>`).join("")}</div>
      <div class="row-actions">
        <button class="secondary" type="button" data-action="use" data-id="${escapeHtml(item.id)}" data-icon="file">Use</button>
        <button type="button" data-action="analyze" data-id="${escapeHtml(item.id)}" data-icon="brain">Analyze</button>
      </div>
    </article>
  `;
}

function renderSource(item) {
  const iconName = sourceIcons[item.name] || "plug";
  return `
    <article class="source-card">
      <div class="source-card-header">
        <div>
          <h4><span data-icon="${escapeHtml(iconName)}" aria-hidden="true"></span> ${escapeHtml(item.name)}</h4>
          <p>${escapeHtml(item.description)}</p>
        </div>
        <span class="status-token status-${escapeHtml(String(item.status).toLowerCase())}"><i></i>${escapeHtml(item.status)}</span>
      </div>
      <dl>
        <div><dt>Mode</dt><dd>${escapeHtml(item.mode)}</dd></div>
        <div><dt>Type</dt><dd>${escapeHtml(item.type)}</dd></div>
        <div><dt>Location</dt><dd>${escapeHtml(item.location)}</dd></div>
      </dl>
    </article>
  `;
}

function renderLoadingAnalysis() {
  analysisStatus.textContent = "Analysis is running. The API returns once retrieval and model or fallback analysis are complete.";
  analysisOutput.innerHTML = renderLoadingSteps();
  hydrateIcons(analysisOutput);
}

function renderLoadingSteps() {
  return `
    <div class="loading-panel">
      <h4><span data-icon="activity" aria-hidden="true"></span> Waiting for analysis</h4>
      <div class="progress-rail" aria-hidden="true"><span></span></div>
      <p>${escapeHtml(loadingSteps[0])}</p>
    </div>
  `;
}

function renderAnalysis(result) {
  const confidence = result.confidence || "Unknown";
  const providerMode = inferProviderMode(result);
  analysisStatus.textContent = `Completed session ${result.sessionId}, turn ${result.sessionTurnNumber}.`;
  analysisOutput.className = "";
  analysisOutput.innerHTML = `
    <div class="analysis-hero">
      <div>
        <span class="status-pill ${providerMode.className}">${escapeHtml(providerMode.label)}</span>
        <h4>${escapeHtml(result.incidentSummary)}</h4>
        <p class="meta">Session ${escapeHtml(result.sessionId)} | Turn ${result.sessionTurnNumber} | ${escapeHtml(formatAnalysisProvider(result))}</p>
      </div>
      ${renderConfidenceMeter(confidence)}
    </div>
    <div class="section-block">
      <h4>Evidence</h4>
      <div class="result-list">${result.retrievedEvidence.map(renderEvidence).join("")}</div>
    </div>
    <div class="section-block">
      <h4>Hypotheses</h4>
      <div class="result-list">${result.rootCauseHypotheses.map(renderHypothesis).join("")}</div>
    </div>
    <div class="section-block">
      <h4>Recommended Actions</h4>
      <div class="result-list">${result.recommendedActions.map(renderAction).join("")}</div>
    </div>
    <div class="section-block">
      <h4>Notes</h4>
      <p>${escapeHtml(formatNotes(result.notes))}</p>
    </div>
  `;
  hydrateIcons(analysisOutput);
}

function renderConfidenceMeter(confidence) {
  const score = confidenceScore(confidence);
  return `
    <div class="confidence-card">
      <span>Confidence</span>
      <strong>${escapeHtml(confidence)}</strong>
      <div class="confidence-track" aria-hidden="true">
        <div style="width: ${score}%"></div>
      </div>
    </div>
  `;
}

function renderEvidence(item) {
  return `
    <article class="result-item evidence">
      <h4><span data-icon="search" aria-hidden="true"></span> ${escapeHtml(formatEvidenceSource(item.source || "evidence"))}</h4>
      <p>${escapeHtml(item.summary)}</p>
      <p class="meta">${escapeHtml(item.details || "")}</p>
    </article>
  `;
}

function renderHypothesis(item) {
  return `
    <article class="result-item hypothesis">
      <h4><span data-icon="brain" aria-hidden="true"></span> ${escapeHtml(item.inferenceStrength)} / ${escapeHtml(item.confidence || "unknown")}</h4>
      <p>${escapeHtml(item.description)}</p>
      <div class="badge-row">${(item.evidenceReferences || []).map((value) => `<span class="badge">${escapeHtml(value)}</span>`).join("")}</div>
    </article>
  `;
}

function renderAction(item) {
  return `
    <article class="result-item action">
      <h4><span data-icon="check" aria-hidden="true"></span> ${escapeHtml(item.priority)}</h4>
      <p>${escapeHtml(item.description)}</p>
      <p class="meta">${escapeHtml(item.rationale || "")}</p>
      <div class="badge-row">${(item.supportingSignals || []).map((value) => `<span class="badge">${escapeHtml(value)}</span>`).join("")}</div>
    </article>
  `;
}

function renderRagMatch(item) {
  return `
    <article class="result-item">
      <h4><span data-icon="book" aria-hidden="true"></span> ${escapeHtml(item.title)}</h4>
      <p>${escapeHtml(item.summary)}</p>
      <p class="meta">${escapeHtml(item.sectionPath || "root")}</p>
      <div class="badge-row">${(item.tags || []).slice(0, 8).map((value) => `<span class="badge">${escapeHtml(value)}</span>`).join("")}</div>
    </article>
  `;
}

function renderEvaluationScenario(item) {
  return `
    <article class="evaluation-card">
      <div class="evaluation-card-header">
        <div>
          <h4><span data-icon="check" aria-hidden="true"></span> ${escapeHtml(item.title)}</h4>
          <p class="meta">${escapeHtml(item.name)} | ${escapeHtml(item.serviceName || "unknown service")} / ${escapeHtml(item.environment || "unknown environment")}</p>
        </div>
        <span class="severity severity-${escapeHtml(String(item.severity).toLowerCase())}">${escapeHtml(item.severity)}</span>
      </div>
      <div class="badge-row">${(item.tags || []).map((value) => `<span class="badge">${escapeHtml(value)}</span>`).join("")}</div>
      <div class="rubric-columns">
        <div><strong><span data-icon="search" aria-hidden="true"></span> Evidence</strong>${renderBadges(item.expectedEvidenceSignals)}</div>
        <div><strong><span data-icon="brain" aria-hidden="true"></span> Hypotheses</strong>${renderBadges(item.expectedHypothesisThemes)}</div>
        <div><strong><span data-icon="check" aria-hidden="true"></span> Actions</strong>${renderBadges(item.expectedActionThemes)}</div>
      </div>
    </article>
  `;
}

function renderBadges(values) {
  return `<div class="badge-row">${(values || []).map((value) => `<span class="badge">${escapeHtml(value)}</span>`).join("")}</div>`;
}

function formatNotes(value) {
  const text = String(value || "").trim();
  if (!text) {
    return "No notes captured.";
  }

  const fixed = text.replaceAll("configureLocal", "configured. Local");
  const sentences = fixed
    .split(/(?<=[.!?])\s+/)
    .map((sentence) => sentence.trim())
    .filter(Boolean);
  return [...new Set(sentences)].join(" ");
}

function fillIncidentForm(item) {
  incidentForm.title.value = formatIncidentTitle(item.title || "");
  incidentForm.description.value = item.description || "";
  incidentForm.severity.value = item.severity || "High";
  incidentForm.serviceName.value = item.serviceName || "";
  incidentForm.environment.value = item.environment || "";
  incidentForm.tags.value = (item.suggestedTags || []).join(", ");
  ragForm.query.value = [item.serviceName, ...(item.suggestedTags || [])].filter(Boolean).join(" ");
}

function formatIncidentTitle(title) {
  return String(title || "")
    .replace("request error rate threshold breached", "error rate threshold")
    .replace("queue depth threshold breached", "queue depth threshold")
    .replace("suspicious log pattern", "log signal");
}

function formatSourceName(source) {
  const value = String(source || "");
  if (value.includes("metrics") && value.includes("logs")) {
    return "Logs + metrics";
  }

  if (value.includes("metrics")) {
    return "Metrics";
  }

  if (value.includes("logs")) {
    return "Logs";
  }

  return value || "Unknown";
}

function formatEvidenceSource(source) {
  return String(source || "evidence")
    .replace("incident.description", "Incident description")
    .replace("incident.tags", "Incident tags")
    .replace("incident.timestamp", "Incident timestamp")
    .replace("tool.logs", "Logs")
    .replace("tool.metrics", "Metrics")
    .replace("agent.local", "Local analysis")
    .replace(/^rag\.runbook\./, "Runbook: ");
}

function inferProviderMode(result) {
  if (result?.usedFallbackAnalysis) {
    return { label: "Local fallback", className: "status-warning" };
  }

  return { label: "Model response", className: "status-connected" };
}

function formatAnalysisProvider(item) {
  const provider = item?.analysisProvider || "unknown provider";
  const model = item?.analysisModel ? ` / ${item.analysisModel}` : "";
  const fallback = item?.usedFallbackAnalysis ? " / fallback" : "";
  return `${provider}${model}${fallback}`;
}

function confidenceScore(confidence) {
  switch (String(confidence || "").toLowerCase()) {
    case "high":
      return 92;
    case "medium":
      return 62;
    case "low":
      return 32;
    default:
      return 12;
  }
}

function setAnalyzing(isAnalyzing) {
  analyzeButton.disabled = isAnalyzing;
  analyzeButton.innerHTML = isAnalyzing
    ? `${iconSvg("activity")}<span>Analyzing...</span>`
    : `${iconSvg("play")}<span>Analyze Incident</span>`;
  if (!isAnalyzing && loadingTimer) {
    window.clearInterval(loadingTimer);
    loadingTimer = null;
  }
}

async function requestJson(url, options = {}) {
  const response = await fetch(url, options);
  const text = await response.text();
  const data = text ? JSON.parse(text) : null;
  if (!response.ok) {
    const detail = data?.detail || data?.title || response.statusText;
    throw new Error(detail);
  }
  return data;
}

function normalizeArray(value) {
  if (Array.isArray(value)) {
    return value;
  }

  if (Array.isArray(value?.value)) {
    return value.value;
  }

  return [];
}

function renderError(target, error) {
  target.innerHTML = `<div class="error-box">${escapeHtml(error.message || String(error))}</div>`;
}

function splitTags(value) {
  return String(value || "")
    .split(",")
    .map((tag) => tag.trim())
    .filter(Boolean);
}

function emptyToNull(value) {
  const text = String(value || "").trim();
  return text.length === 0 ? null : text;
}

function formatDate(value) {
  return value ? new Date(value).toLocaleString() : "unknown time";
}

function shortenId(value) {
  const text = String(value || "");
  return text.length > 12 ? `${text.slice(0, 8)}...` : text || "none";
}

function showToast(title, message, tone = "info") {
  const toast = document.createElement("div");
  toast.className = `toast toast-${tone}`;
  toast.innerHTML = `
    <strong>${escapeHtml(title)}</strong>
    <span>${escapeHtml(message)}</span>
  `;
  toastRegion.appendChild(toast);
  window.setTimeout(() => {
    toast.classList.add("leaving");
    window.setTimeout(() => toast.remove(), 240);
  }, 3600);
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function requireElement(selector) {
  const element = document.querySelector(selector);
  if (!element) {
    throw new Error(`Frontend initialization failed: missing ${selector}`);
  }

  return element;
}

function iconSvg(name) {
  const path = iconPaths[name];
  if (!path) {
    return "";
  }

  return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${path}</svg>`;
}

function hydrateIcons(root = document) {
  const elements = [];
  if (root.matches?.("[data-icon]")) {
    elements.push(root);
  }

  if (root.querySelectorAll) {
    elements.push(...root.querySelectorAll("[data-icon]"));
  }

  elements.forEach((element) => {
    const iconName = element.dataset.icon;
    const label = Array.from(element.childNodes)
      .filter((node) => node.nodeType === Node.TEXT_NODE)
      .map((node) => node.textContent)
      .join("")
      .trim();
    const svg = iconSvg(iconName);
    if (!svg || element.dataset.iconHydrated === iconName) {
      return;
    }

    element.innerHTML = label ? `${svg}<span>${escapeHtml(label)}</span>` : svg;
    element.classList.add("svg-ready");
    element.dataset.iconHydrated = iconName;
  });
}

function getStoredTheme() {
  try {
    return window.localStorage.getItem("incident-response-theme");
  } catch {
    return null;
  }
}

function setStoredTheme(theme) {
  try {
    window.localStorage.setItem("incident-response-theme", theme);
  } catch {
    // Storage can be blocked in some browser privacy modes. Theme still changes for this page load.
  }
}

function initializeApp() {
  hydrateIcons();
  applyTheme(getStoredTheme() || "light");
  activateTab("monitor");
  checkHealth();
  loadSources();
  loadDetected({ userInitiated: true });
  searchRag();
  loadRecent();
  loadEvaluation();
  window.setInterval(loadDetected, 30000);
}

initializeApp();
