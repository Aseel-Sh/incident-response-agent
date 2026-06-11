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

const tabCopy = {
  monitor: ["Monitor", "Review incidents detected from the currently connected signal sources."],
  analyze: ["Analyze", "Submit an incident manually or analyze a detected candidate."],
  runbooks: ["Runbooks", "Search the RAG knowledge base and inspect retrieval status."],
  history: ["History", "Review saved incident analyses from previous runs."],
  sources: ["Sources", "See exactly what logs, metrics, runbooks, and vector settings the app is using."]
};

const severityRank = { Critical: 4, High: 3, Medium: 2, Low: 1, Unknown: 0 };
const loadingSteps = [
  "Collecting incident details",
  "Retrieving runbooks",
  "Searching logs and metrics",
  "Waiting for model or local fallback",
  "Formatting recommendations"
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

  const [title, description] = tabCopy[tabName] || tabCopy.monitor;
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
  themeToggle.textContent = theme === "dark" ? "Light Mode" : "Dark Mode";
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
    detectedOutput.innerHTML = `<div class="empty-state">Scanning sample logs and metrics...</div>`;
  }

  try {
    detectedCandidates = normalizeArray(await requestJson("/api/incidents/detected"));
    lastScan.textContent = `Last scan ${new Date().toLocaleTimeString()}`;
    updateAnalytics();
    renderDetectedCandidates();
  } catch (error) {
    renderError(detectedOutput, error);
  }
}

async function loadSources() {
  try {
    sourceRows = normalizeArray(await requestJson("/api/operations/sources"));
    sourcesOutput.innerHTML = sourceRows.map(renderSource).join("");
  } catch (error) {
    renderError(sourcesOutput, error);
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
  } catch (error) {
    renderError(analysisOutput, error);
    analysisStatus.textContent = "Analysis failed. Check the error below.";
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
      <span>Embeddings: <strong>${escapeHtml(result.embeddingProvider)}</strong> / ${escapeHtml(result.embeddingModel)}</span>
      <span>Vector store: <strong>${escapeHtml(result.vectorStoreProvider || "sqlite")}</strong>${result.vectorStoreCollection ? ` / ${escapeHtml(result.vectorStoreCollection)}` : ""}</span>
      <span>Matches: <strong>${result.matches.length}</strong></span>
    `;
    ragResults.innerHTML = result.matches.map(renderRagMatch).join("") || `<div class="empty-state">No matches.</div>`;
  } catch (error) {
    renderError(ragResults, error);
  }
}

async function loadRecent() {
  recentOutput.innerHTML = `<div class="empty-state">Loading recent analyses...</div>`;
  try {
    const results = await requestJson("/api/incidents/recent?maxResults=8");
    recentOutput.innerHTML = results.map((item) => `
      <article class="result-item">
        <h4>${escapeHtml(item.incidentSummary)}</h4>
        <p class="meta">Session ${escapeHtml(item.sessionId)} | Turn ${item.sessionTurnNumber} | ${escapeHtml(item.confidence || "unknown")} confidence</p>
        <p>${escapeHtml(item.notes || "")}</p>
      </article>
    `).join("") || `<div class="empty-state">No saved analyses yet.</div>`;
  } catch (error) {
    renderError(recentOutput, error);
  }
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
        <button class="secondary" type="button" data-action="use" data-id="${escapeHtml(item.id)}">Use</button>
        <button type="button" data-action="analyze" data-id="${escapeHtml(item.id)}">Analyze</button>
      </div>
    </article>
  `;
}

function renderSource(item) {
  return `
    <article class="source-card">
      <div class="source-card-header">
        <div>
          <h4>${escapeHtml(item.name)}</h4>
          <p>${escapeHtml(item.description)}</p>
        </div>
        <span class="status-pill status-${escapeHtml(String(item.status).toLowerCase())}">${escapeHtml(item.status)}</span>
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
  let currentStep = 0;
  analysisStatus.textContent = "Analysis started. Free hosted models can be slow; local fallback takes over on timeout.";
  analysisOutput.innerHTML = renderLoadingSteps(currentStep);
  loadingTimer = window.setInterval(() => {
    currentStep = Math.min(currentStep + 1, loadingSteps.length - 1);
    analysisOutput.innerHTML = renderLoadingSteps(currentStep);
  }, 1200);
}

function renderLoadingSteps(currentStep) {
  return `
    <div class="loading-panel">
      <h4>Analyzing incident</h4>
      <ol>
        ${loadingSteps.map((step, index) => `<li class="${index <= currentStep ? "active" : ""}">${escapeHtml(step)}</li>`).join("")}
      </ol>
    </div>
  `;
}

function renderAnalysis(result) {
  const confidence = result.confidence || "Unknown";
  const providerMode = inferProviderMode(result.notes);
  analysisStatus.textContent = `Completed session ${result.sessionId}, turn ${result.sessionTurnNumber}.`;
  analysisOutput.className = "";
  analysisOutput.innerHTML = `
    <div class="analysis-hero">
      <div>
        <span class="status-pill ${providerMode.className}">${escapeHtml(providerMode.label)}</span>
        <h4>${escapeHtml(result.incidentSummary)}</h4>
        <p class="meta">Session ${escapeHtml(result.sessionId)} | Turn ${result.sessionTurnNumber}</p>
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
      <p>${escapeHtml(result.notes || "")}</p>
    </div>
  `;
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
    <article class="result-item">
      <h4>${escapeHtml(item.source || "evidence")}</h4>
      <p>${escapeHtml(item.summary)}</p>
      <p class="meta">${escapeHtml(item.details || "")}</p>
    </article>
  `;
}

function renderHypothesis(item) {
  return `
    <article class="result-item">
      <h4>${escapeHtml(item.inferenceStrength)} | ${escapeHtml(item.confidence || "unknown")}</h4>
      <p>${escapeHtml(item.description)}</p>
      <div class="badge-row">${(item.evidenceReferences || []).map((value) => `<span class="badge">${escapeHtml(value)}</span>`).join("")}</div>
    </article>
  `;
}

function renderAction(item) {
  return `
    <article class="result-item">
      <h4>${escapeHtml(item.priority)}</h4>
      <p>${escapeHtml(item.description)}</p>
      <p class="meta">${escapeHtml(item.rationale || "")}</p>
      <div class="badge-row">${(item.supportingSignals || []).map((value) => `<span class="badge">${escapeHtml(value)}</span>`).join("")}</div>
    </article>
  `;
}

function renderRagMatch(item) {
  return `
    <article class="result-item">
      <h4>${escapeHtml(item.title)}</h4>
      <p>${escapeHtml(item.summary)}</p>
      <p class="meta">Score ${item.score} | ${escapeHtml(item.sectionPath || "root")}</p>
      <div class="badge-row">${(item.tags || []).slice(0, 8).map((value) => `<span class="badge">${escapeHtml(value)}</span>`).join("")}</div>
    </article>
  `;
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

function inferProviderMode(notes) {
  const text = String(notes || "").toLowerCase();
  if (text.includes("local prompt-based fallback")) {
    return { label: "Local fallback", className: "status-warning" };
  }

  return { label: "Model response", className: "status-connected" };
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
  analyzeButton.textContent = isAnalyzing ? "Analyzing..." : "Analyze Incident";
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
  applyTheme(getStoredTheme() || "light");
  activateTab("monitor");
  checkHealth();
  loadSources();
  loadDetected({ userInitiated: true });
  searchRag();
  loadRecent();
  window.setInterval(loadDetected, 30000);
}

initializeApp();
