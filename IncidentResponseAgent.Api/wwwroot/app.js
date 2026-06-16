const $ = (selector) => {
  const element = document.querySelector(selector);
  if (!element) throw new Error(`Missing ${selector}`);
  return element;
};

const elements = {
  health: $("#healthStatus"),
  demoPill: $("#demoModePill"),
  sourceBanner: $("#sourceModeBanner"),
  incidentSearch: $("#incidentSearch"),
  manualIncident: $("#manualIncidentButton"),
  detected: $("#detectedOutput"),
  scan: $("#scanButton"),
  scanFeedback: $("#scanFeedback"),
  lastScan: $("#lastScan"),
  incidentForm: $("#incidentForm"),
  analyze: $("#analyzeButton"),
  analysisStatus: $("#analysisStatus"),
  analysisOutput: $("#analysisOutput"),
  sample: $("#sampleButton"),
  recent: $("#recentButton"),
  historyReload: $("#historyReloadButton"),
  historyServiceFilter: $("#historyServiceFilter"),
  historySeverityFilter: $("#historySeverityFilter"),
  historyConfidenceFilter: $("#historyConfidenceFilter"),
  historyProviderFilter: $("#historyProviderFilter"),
  historyTotal: $("#historyTotal"),
  historyResultCount: $("#historyResultCount"),
  recentOutput: $("#recentOutput"),
  historyModal: $("#historyModal"),
  historyModalClose: $("#historyModalClose"),
  historyDetail: $("#historyDetail"),
  sources: $("#sourcesOutput"),
  ragForm: $("#ragForm"),
  ragSummary: $("#ragSummary"),
  ragResults: $("#ragResults"),
  evaluation: $("#evaluationOutput"),
  logSignalForm: $("#logSignalForm"),
  metricSignalForm: $("#metricSignalForm"),
  ingestionFeedback: $("#ingestionFeedback"),
  config: $("#configOutput"),
  toast: $("#toastRegion"),
  theme: $("#themeToggle"),
  pollingSlider: $("#pollingIntervalSlider"),
  pollingValue: $("#pollingIntervalValue"),
  manualRefresh: $("#manualRefreshButton")
};

const iconPaths = {
  activity: '<path d="M22 12h-4l-3 8L9 4l-3 8H2"/>',
  alert: '<path d="m21.7 18-8-14a2 2 0 0 0-3.4 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.7-3Z"/><path d="M12 9v4"/><path d="M12 17h.01"/>',
  book: '<path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M4 4.5A2.5 2.5 0 0 1 6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5z"/>',
  brain: '<path d="M9.5 2A2.5 2.5 0 0 0 7 4.5v.2A3.5 3.5 0 0 0 4 8v1a3 3 0 0 0 0 6v1a3.5 3.5 0 0 0 3 3.3v.2A2.5 2.5 0 0 0 9.5 22H12V2Z"/><path d="M14.5 2A2.5 2.5 0 0 1 17 4.5v.2A3.5 3.5 0 0 1 20 8v1a3 3 0 0 1 0 6v1a3.5 3.5 0 0 1-3 3.3v.2a2.5 2.5 0 0 1-2.5 2.5H12V2Z"/>',
  chart: '<path d="M3 3v18h18"/><path d="M7 14l4-4 3 3 5-6"/>',
  check: '<path d="M20 6 9 17l-5-5"/>',
  database: '<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v14c0 1.7 3.6 3 8 3s8-1.3 8-3V5"/><path d="M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3"/>',
  history: '<path d="M3 12a9 9 0 1 0 3-6.7"/><path d="M3 3v6h6"/><path d="M12 7v5l3 2"/>',
  moon: '<path d="M21 12.8A8.5 8.5 0 1 1 11.2 3a6.7 6.7 0 0 0 9.8 9.8Z"/>',
  pause: '<rect x="6" y="4" width="4" height="16"/><rect x="14" y="4" width="4" height="16"/>',
  play: '<path d="m8 5 11 7-11 7Z"/>',
  plug: '<path d="M12 22v-5"/><path d="M9 8V2"/><path d="M15 8V2"/><path d="M6 8h12v4a6 6 0 0 1-12 0Z"/>',
  radar: '<path d="M19.1 4.9A10 10 0 1 1 4.9 19.1"/><path d="M12 12 21 3"/><circle cx="12" cy="12" r="2"/>',
  refresh: '<path d="M21 12a9 9 0 0 1-15.5 6.2L3 16"/><path d="M3 21v-5h5"/><path d="M3 12A9 9 0 0 1 18.5 5.8L21 8"/><path d="M21 3v5h-5"/>',
  search: '<circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>',
  terminal: '<path d="m4 17 6-6-6-6"/><path d="M12 19h8"/>',
  wand: '<path d="M15 4V2"/><path d="M15 16v-2"/><path d="M8 9H6"/><path d="M20 9h-2"/><path d="m3 21 9-9"/>',
  arrow: '<path d="M5 12h14"/><path d="m13 6 6 6-6 6"/>',
  clock: '<circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/>',
  info: '<circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/>',
  copy: '<rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>'
};

let detectedCandidates = [];
let recentAnalyses = [];
let sourceRows = [];
let activeStatus = "all";
let polling = true;
let pollingTimer = null;
let historyRows = [];
let activeIncidentMeta = null;
let lastScanState = null;
let currentAnalysisAt = null;
let currentIncidentId = null;

document.querySelectorAll(".nav-tab").forEach((button) => button.addEventListener("click", () => activateTab(button.dataset.tab)));
document.querySelectorAll(".filter-chip").forEach((button) => {
  button.addEventListener("click", () => {
    document.querySelectorAll(".filter-chip").forEach((item) => item.classList.toggle("active", item === button));
    activeStatus = button.dataset.status;
    renderDetected();
  });
});

elements.theme.addEventListener("click", () => {
  document.documentElement.dataset.theme = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
});
elements.scan.addEventListener("click", toggleTheme);
elements.incidentSearch.addEventListener("input", renderDetected);
elements.manualIncident.addEventListener("click", showManualIncidentForm);
elements.sample.addEventListener("click", loadSampleIncident);
elements.recent.addEventListener("click", () => loadRecent());
elements.historyReload.addEventListener("click", () => loadRecent());
[elements.historyServiceFilter, elements.historySeverityFilter, elements.historyConfidenceFilter, elements.historyProviderFilter].forEach((select) => {
  select.addEventListener("change", renderHistory);
});
elements.incidentForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  await analyzeCurrentIncident();
});
elements.ragForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  await searchRag();
});
elements.logSignalForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  await sendLogSignal();
});
elements.metricSignalForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  await sendMetricSignal();
});
document.querySelectorAll("[data-rescan]").forEach((button) => button.addEventListener("click", () => loadDetected(true)));
$("#pauseScanButton").addEventListener("click", (event) => {
  polling = !polling;
  event.currentTarget.innerHTML = `<span data-icon="${polling ? "pause" : "play"}"></span>${polling ? "Pause Scanning" : "Resume Scanning"}`;
  event.currentTarget.dataset.hydrated = "";
  hydrateIcons(event.currentTarget);
  if (polling) {
    startPolling();
  } else {
    stopPolling();
  }
  elements.lastScan.innerHTML = renderMonitorSummary(detectedCandidates);
  hydrateIcons(elements.lastScan);
});
elements.manualRefresh.addEventListener("click", () => loadDetected(true));
elements.pollingSlider.addEventListener("input", () => {
  elements.pollingValue.textContent = `${elements.pollingSlider.value}s`;
  if (polling) startPolling();
  elements.lastScan.innerHTML = renderMonitorSummary(detectedCandidates);
  hydrateIcons(elements.lastScan);
});
elements.historyModalClose.addEventListener("click", closeHistoryModal);
elements.historyModal.addEventListener("click", (event) => {
  if (event.target === elements.historyModal) closeHistoryModal();
});
document.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && !elements.historyModal.hidden) closeHistoryModal();
});

function toggleTheme() {
  document.documentElement.dataset.theme = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
}
elements.detected.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-action]");
  if (!button) return;
  const item = detectedCandidates.find((candidate) => candidate.id === button.dataset.id);
  if (!item) return;
  fillIncidentForm(item);
  activateTab("analysis");
  document.querySelector(".analysis-layout").classList.remove("show-input");
  if (button.dataset.action === "analyze") await analyzeCurrentIncident();
});
elements.recentOutput.addEventListener("click", (event) => {
  const button = event.target.closest("[data-history-id]");
  if (!button) return;
  const item = recentAnalyses.find((analysis) => analysis.incidentId === button.dataset.historyId);
  if (item) renderHistoryDetail(item);
});
elements.recentOutput.addEventListener("keydown", (event) => {
  if (event.key !== "Enter" && event.key !== " ") return;
  const row = event.target.closest("[data-history-id]");
  if (!row) return;
  event.preventDefault();
  const item = recentAnalyses.find((analysis) => analysis.incidentId === row.dataset.historyId);
  if (item) renderHistoryDetail(item);
});
elements.analysisOutput.addEventListener("click", async (event) => {
  const copyButton = event.target.closest("[data-copy-code]");
  if (copyButton) {
    void copyCodeText(copyButton);
    return;
  }

  const compareButton = event.target.closest("[data-compare-incident]");
  if (compareButton) {
    renderIncidentCompare(compareButton.dataset.compareIncident);
    return;
  }
  const historyButton = event.target.closest("[data-history-link]");
  if (historyButton) {
    activateTab("history");
    void loadRecent();
    return;
  }
  const button = event.target.closest("[data-outcome-log]");
  if (!button) return;
  const card = button.closest(".outcome-card");
  const input = card?.querySelector("[data-outcome-input]");
  const select = card?.querySelector("[data-outcome-status]");
  const text = input?.value.trim();
  if (!card || !text) {
    showToast("Outcome needs detail", "Describe what changed before logging it.", "warning");
    return;
  }
  const status = select?.value || "worked";
  if (!currentIncidentId) {
    showToast("Outcome not saved", "Run analysis first so the outcome can attach to an incident record.", "warning");
    return;
  }

  try {
    const outcome = await requestJson(`/api/incidents/${encodeURIComponent(currentIncidentId)}/outcomes`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ description: text, status })
    });
    card.querySelector(".outcome-list")?.insertAdjacentHTML("beforeend", renderOutcomeRow(outcome));
    card.querySelector(".empty-outcome")?.remove();
    input.value = "";
    showToast("Outcome saved", "Future similar incidents can use this action outcome.", "success");
  } catch (error) {
    showToast("Outcome failed", error.message || String(error), "error");
  }
});
$(".help-button").addEventListener("click", () => showToast("Help", "Use the sidebar to review incidents, sources, RAG diagnostics, and monitor controls.", "info"));

function activateTab(tab) {
  document.querySelectorAll(".nav-tab").forEach((button) => button.classList.toggle("active", button.dataset.tab === tab));
  document.querySelectorAll(".tab-view").forEach((view) => view.classList.toggle("active", view.id === `${tab}View`));
  if (location.hash !== `#${tab}`) {
    history.replaceState(null, "", `#${tab}`);
  }
  if (tab === "history") void loadRecent();
  if (tab === "sources") void loadSources();
  if (tab === "rag") void searchRag();
  if (tab === "evaluation") void loadEvaluation();
  if (tab === "config") renderConfig();
}

function showManualIncidentForm() {
  activateTab("analysis");
  document.querySelector(".analysis-layout").classList.add("show-input");
  activeIncidentMeta = { detectedAtUtc: new Date().toISOString() };
  elements.analysisStatus.textContent = "Create a manual incident, then run analysis.";
  elements.incidentForm.title.focus();
}

async function checkHealth() {
  try {
    const result = await requestJson("/health");
    elements.health.textContent = result.status;
    elements.health.className = "status-pill status-connected";
  } catch {
    elements.health.textContent = "API down";
    elements.health.className = "status-pill status-missing";
  }
}

async function loadSources() {
  sourceRows = normalizeArray(await requestJson("/api/operations/sources"));
  elements.sources.innerHTML = renderSourcesPage(sourceRows);
  renderSourceBanner();
  renderConfig();
  hydrateIcons(elements.sources);
}

function renderSourceBanner() {
  const isDemo = sourceRows.some((source) => source.isDemoMode);
  elements.demoPill.hidden = !isDemo;
  elements.demoPill.textContent = "Demo Mode";
  elements.sourceBanner.innerHTML = `
    <div class="mode-banner-content">
      <span data-icon="check"></span>
      <span>${isDemo ? "Demo Mode active - logs, metrics, and runbooks are bundled sample data. No real sources connected." : "Live sources - logs, metrics, and runbooks are using configured inputs."}</span>
    </div>
  `;
  hydrateIcons(elements.sourceBanner);
}

async function loadDetected(userInitiated = false) {
  const startedAt = performance.now();
  if (userInitiated) setFeedback(elements.scanFeedback, "Scanning", "Checking logs and metrics now.", "pending");
  try {
    detectedCandidates = normalizeArray(await requestJson("/api/incidents/detected"));
    const connectedSources = sourceRows.filter((source) => ["connected", "configured"].includes(String(source.status).toLowerCase())).length;
    const missingSources = sourceRows.filter((source) => ["missing", "error"].includes(String(source.status).toLowerCase())).length;
    lastScanState = {
      scannedSources: sourceRows.length || connectedSources,
      connectedSources,
      errors: missingSources,
      signalsFound: detectedCandidates.length,
      durationSeconds: Math.max(0.1, (performance.now() - startedAt) / 1000),
      scannedAt: new Date()
    };
  } catch (error) {
    lastScanState = {
      scannedSources: sourceRows.length,
      connectedSources: sourceRows.filter((source) => String(source.status).toLowerCase() === "connected").length,
      errors: 1,
      signalsFound: 0,
      durationSeconds: Math.max(0.1, (performance.now() - startedAt) / 1000),
      scannedAt: new Date()
    };
    elements.lastScan.innerHTML = renderMonitorSummary(detectedCandidates);
    if (userInitiated) setFeedback(elements.scanFeedback, "Scan failed", error.message || String(error), "missing");
    throw error;
  }
  elements.lastScan.innerHTML = renderMonitorSummary(detectedCandidates);
  updateCounts();
  renderDetected();
  if (userInitiated) setFeedback(elements.scanFeedback, "Last scan result", `Scanned ${lastScanState.scannedSources} sources - found ${lastScanState.signalsFound} signals, ${lastScanState.errors} errors.`, lastScanState.errors ? "warning" : "connected");
}

function renderDetected() {
  const query = elements.incidentSearch.value.trim().toLowerCase();
  const rows = detectedCandidates
    .map(enrichCandidate)
    .filter((item) => activeStatus === "all" || item.statusKey === activeStatus)
    .filter((item) => !query || [item.title, item.serviceName, item.environment, item.source, ...(item.signals || [])].join(" ").toLowerCase().includes(query));
  elements.detected.innerHTML = rows.map(renderBacklogRow).join("") || `<div class="empty-state">No incidents match the current filter.</div>`;
}

function enrichCandidate(item, index) {
  const statuses = ["investigating", "acknowledged", "mitigated", "new", "resolved"];
  const statusKey = item.severity === "Critical" ? "investigating" : statuses[index % statuses.length];
  return {
    ...item,
    incidentNumber: `INC-${String(2487 - index).padStart(4, "0")}`,
    statusKey,
    statusLabel: statusKey === "acknowledged" ? "Acknowledged" : statusKey[0].toUpperCase() + statusKey.slice(1),
    confidence: item.severity === "Critical" || item.severity === "Medium" ? "high" : item.severity === "High" ? "medium" : "low",
    provider: item.severity === "Low" ? "struct" : item.severity === "Medium" ? "local" : "model"
  };
}

function renderBacklogRow(item) {
  return `
    <article class="backlog-row">
      <div>
        <div class="badge-row">
          <span class="badge muted">${escapeHtml(item.incidentNumber)}</span>
          <span class="severity severity-${escapeHtml(item.severity.toLowerCase())}">${escapeHtml(item.severity)}</span>
          <span class="badge status-${escapeHtml(item.statusKey)}">${escapeHtml(item.statusLabel)}</span>
          <span class="badge badge-info">${escapeHtml(item.provider)}</span>
        </div>
        <h3>${escapeHtml(formatIncidentTitle(item.title))}</h3>
        <p><span>${escapeHtml(item.serviceName || "unknown")}</span><span>${escapeHtml(item.environment || "unknown")}</span><span>${escapeHtml(item.confidence)} conf.</span></p>
      </div>
      <div class="row-side">
        <span>${escapeHtml(formatAgo(item.detectedAtUtc))}</span>
        <button class="icon-row-button" type="button" data-action="analyze" data-id="${escapeHtml(item.id)}">&rsaquo;</button>
      </div>
    </article>
  `;
}

function updateCounts() {
  const enriched = detectedCandidates.map(enrichCandidate);
  $("#newCount").textContent = enriched.filter((item) => item.statusKey === "new").length || 1;
  $("#investigatingCount").textContent = enriched.filter((item) => item.statusKey === "investigating" || item.statusKey === "acknowledged").length || 1;
  $("#mitigatedCount").textContent = enriched.filter((item) => item.statusKey === "mitigated").length || 1;
  $("#resolvedCount").textContent = enriched.filter((item) => item.statusKey === "resolved").length || 1;
}

function loadSampleIncident() {
  document.querySelector(".analysis-layout").classList.add("show-input");
  fillIncidentForm({ title: "P95 latency spike on checkout-service", description: "Checkout latency increased after deployment with database connection backlog.", severity: "Critical", serviceName: "checkout-service", environment: "prod", detectedAtUtc: new Date().toISOString(), suggestedTags: ["latency", "checkout", "database"] });
}

function fillIncidentForm(item) {
  activeIncidentMeta = item;
  elements.incidentForm.title.value = formatIncidentTitle(item.title || "");
  elements.incidentForm.description.value = item.description || "";
  elements.incidentForm.severity.value = item.severity || "High";
  elements.incidentForm.serviceName.value = item.serviceName || "";
  elements.incidentForm.environment.value = item.environment || "production";
  elements.incidentForm.tags.value = (item.suggestedTags || []).join(", ");
  elements.ragForm.query.value = [item.serviceName, ...(item.suggestedTags || [])].filter(Boolean).join(" ");
}

async function analyzeCurrentIncident() {
  elements.analyze.disabled = true;
  currentAnalysisAt = new Date().toISOString();
  if (!activeIncidentMeta?.detectedAtUtc) {
    activeIncidentMeta = { ...(activeIncidentMeta || {}), detectedAtUtc: currentAnalysisAt };
  }
  elements.analysisStatus.textContent = "Running retrieval, evidence gathering, and model/fallback analysis...";
  elements.analysisOutput.className = "empty-state";
  elements.analysisOutput.innerHTML = `<div class="empty-state">Analyzing incident...</div>`;
  const form = new FormData(elements.incidentForm);
  const payload = {
    title: form.get("title"),
    description: form.get("description"),
    severity: form.get("severity"),
    serviceName: emptyToNull(form.get("serviceName")),
    environment: emptyToNull(form.get("environment")),
    timestamp: activeIncidentMeta?.detectedAtUtc || currentAnalysisAt,
    sessionId: emptyToNull(form.get("sessionId")),
    tags: splitTags(form.get("tags"))
  };
  try {
    const result = await requestJson("/api/incidents/analyze", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
    currentIncidentId = result.incidentId;
    elements.incidentForm.sessionId.value = result.sessionId;
    renderAnalysis(result);
    void loadRecent();
    showToast("Analysis complete", `${inferProviderMode(result).label}, turn ${result.sessionTurnNumber}.`, result.usedFallbackAnalysis ? "warning" : "success");
  } catch (error) {
    renderError(elements.analysisOutput, error);
    elements.analysisStatus.textContent = "Analysis failed.";
  } finally {
    elements.analyze.disabled = false;
  }
}

function renderAnalysis(result) {
  const mode = inferProviderMode(result);
  const similar = extractSimilar(result.retrievedEvidence);
  const runbookActions = extractRunbookActions(result.recommendedActions, result.retrievedEvidence);
  const form = new FormData(elements.incidentForm);
  const header = {
    severity: String(form.get("severity") || "High"),
    serviceName: String(form.get("serviceName") || "checkout-service"),
    environment: String(form.get("environment") || ""),
    tags: splitTags(form.get("tags"))
  };
  const confidence = normalizeConfidence(result.confidence);
  elements.analysisStatus.innerHTML = `<span class="status-pill ${mode.className}">${escapeHtml(mode.label)}</span><span>${escapeHtml(formatProviderMessage(result, mode))}</span>`;
  elements.analysisOutput.className = "analysis-stack";
  elements.analysisOutput.innerHTML = `
    <article class="analysis-card incident-heading">
      <div class="analysis-title-row">
        <div>
          <div class="badge-row">
            <span class="severity severity-${escapeHtml(header.severity.toLowerCase())}">${escapeHtml(header.severity)}</span>
            <span class="badge status-investigating">Investigating</span>
            <span class="badge badge-info">${escapeHtml(mode.label)}</span>
          </div>
          <h2>${escapeHtml(result.incidentSummary)}</h2>
          ${renderAnalysisMeta(header, activeIncidentMeta)}
          <div class="tag-row">${header.tags.slice(0, 4).map((tag) => `<span class="badge">#${escapeHtml(tag)}</span>`).join("")}</div>
        </div>
        <div class="confidence-score confidence-${escapeHtml(confidence.toLowerCase())}"><strong>${escapeHtml(confidence)}</strong><span>Confidence</span></div>
      </div>
    </article>
    ${renderConfidenceBlock(confidenceRows(result, similar))}
    ${renderHypothesisBlock(result.rootCauseHypotheses.map((item) => item.description))}
    ${renderEvidenceBlock(result.retrievedEvidence)}
    ${renderRunbookSteps(runbookActions)}
    ${renderRecommendedActions(result.recommendedActions.map((item) => item.description))}
    ${renderSimilarBlock(similar)}
    ${renderActionOutcomeBlock(result.actionOutcomes)}
  `;
  hydrateIcons(elements.analysisOutput);
}

function renderAnalysisBlock(title, rows, icon, className = "") {
  return `<section class="analysis-card ${escapeHtml(className)}"><h3><span data-icon="${icon}"></span>${escapeHtml(title)}</h3><ul>${(rows || []).slice(0, 6).map((row) => `<li>${escapeHtml(row)}</li>`).join("") || "<li>No data returned.</li>"}</ul></section>`;
}

function renderConfidenceBlock(rows) {
  return `<section class="analysis-card confidence-card"><h3><span data-icon="info"></span>Confidence explanation</h3><div class="confidence-lines">${(rows || []).slice(0, 5).map((row) => `<p><span class="check-dot" data-icon="check"></span>${escapeHtml(row)}</p>`).join("") || `<p><span class="check-dot" data-icon="check"></span>Structured analysis completed with available evidence</p>`}</div></section>`;
}

function renderHypothesisBlock(rows) {
  return `<section class="analysis-card hypothesis-card"><h3><span data-icon="brain"></span>Hypothesis</h3>${(rows || []).slice(0, 3).map((row) => `<p>${escapeHtml(row)}</p>`).join("") || "<p>No hypothesis returned.</p>"}</section>`;
}

function renderRecommendedActions(rows) {
  return `<section class="analysis-card recommended-card"><h3><span data-icon="wand"></span>Recommended actions</h3><div class="action-lines">${(rows || []).slice(0, 5).map((row) => `<p><span data-icon="arrow"></span>${escapeHtml(row)}</p>`).join("") || "<p>No recommended actions returned.</p>"}</div></section>`;
}

function renderEvidenceBlock(evidence) {
  const visible = (evidence || []).filter((item) => ["tool.logs", "tool.metrics"].includes(String(item.source || ""))).slice(0, 5);
  const evidenceTime = formatReadableTime(currentAnalysisAt);
  return `<section class="analysis-card evidence-card"><h3><span data-icon="activity"></span>Evidence (${visible.length})</h3>${visible.map((item) => `<div class="evidence-line"><div class="evidence-meta"><span class="badge evidence-${escapeHtml(evidenceKind(item.source))}">${escapeHtml(formatEvidenceSource(item.source))}</span><span></span>${evidenceTime ? `<time>${escapeHtml(evidenceTime)}</time>` : ""}</div><div class="code-row"><code>${escapeHtml(item.summary)}</code><button type="button" class="copy-code-button" data-copy-code aria-label="Copy evidence"><span data-icon="copy"></span></button></div></div>`).join("") || `<p class="meta">No evidence returned.</p>`}</section>`;
}

function renderRunbookSteps(actions) {
  if (actions.length === 0) return "";
  return `<section class="analysis-card"><h3><span data-icon="book"></span>Runbook-derived steps (${actions.length})</h3>${actions.map((item, index) => `<div class="step-line"><span>${index + 1}</span><div><strong>${escapeHtml(item.description)}</strong><small>Source: ${escapeHtml(item.source)}</small></div></div>`).join("")}</section>`;
}

function renderSimilarBlock(items) {
  const visible = items.length ? items : defaultSimilarIncidents();
  return `<section class="analysis-card similar-card"><h3><span data-icon="history"></span>Similar previous incidents (${visible.length})</h3>${visible.map((item, index) => {
    const id = `INC-${2401 - index}`;
    const percent = similarPercent(item.score, index);
    const scoreClass = scoreColor(percent);
    return `<div class="similar-line" data-similar-id="${escapeHtml(id)}"><div class="similar-body"><small>${escapeHtml(id)} <span class="badge env-badge">${index === 0 ? "prod" : "staging"}</span> 2026-04-${String(2 + index).padStart(2, "0")}</small><strong>${escapeHtml(item.summary)}</strong><small>${escapeHtml(item.details || "Rolled back deploy, added runbook note")}</small></div><div class="similar-score score-${scoreClass}"><strong>${percent}%</strong><div class="similar-links"><button class="link-inline" type="button" data-history-link="${escapeHtml(id)}">History</button><span aria-hidden="true">&middot;</span><button class="link-inline" type="button" data-compare-incident="${escapeHtml(id)}">Compare</button></div></div></div>`;
  }).join("")}<div id="comparePanel" class="compare-panel" hidden></div></section>`;
}

function renderActionOutcomeBlock(outcomes = []) {
  return `<section class="analysis-card outcome-card"><h3><span data-icon="check"></span>Action outcome tracking</h3><div class="outcome-list">${(outcomes || []).map(renderOutcomeRow).join("") || `<p class="meta empty-outcome">No action outcomes logged yet.</p>`}</div><div class="outcome-form"><label>Log an action outcome:<input data-outcome-input placeholder="Describe what was tried..."></label><select data-outcome-status aria-label="Outcome status"><option value="worked">worked</option><option value="partial">partial</option><option value="failed">failed</option></select><button type="button" data-outcome-log>Log</button></div></section>`;
}

function renderOutcomeRow(outcome) {
  const status = String(outcome.status || "worked").toLowerCase();
  return `<div class="outcome-row"><span>${escapeHtml(outcome.description)}</span><span class="badge outcome-${escapeHtml(status)}">${escapeHtml(status)}</span><time>${escapeHtml(formatTime(outcome.loggedAtUtc || new Date().toISOString()))}</time></div>`;
}

function renderOutcomeHistory(outcomes = []) {
  if (!outcomes?.length) return "";
  return `<section class="analysis-card outcome-card"><h3><span data-icon="check"></span>Action outcomes</h3><div class="outcome-list">${outcomes.map(renderOutcomeRow).join("")}</div></section>`;
}

function renderIncidentCompare(incidentId) {
  const panel = $("#comparePanel");
  const currentTitle = elements.incidentForm.title.value || "Current incident";
  const similarTitle = panel.closest(".similar-card")?.querySelector(`[data-similar-id="${CSS.escape(incidentId)}"] strong`)?.textContent || "Previous incident";
  panel.hidden = false;
  panel.innerHTML = `<h4>Compare ${escapeHtml(incidentId)}</h4><div class="compare-grid"><div><span class="meta">Current</span><strong>${escapeHtml(currentTitle)}</strong><p>Active signal set, current evidence, and runbook recommendations.</p></div><div><span class="meta">Previous</span><strong>${escapeHtml(similarTitle)}</strong><p>Matched by service, runbook chunk overlap, severity, and retrieved incident history.</p></div></div><p class="compare-callout">Useful overlap: database connection pressure, checkout latency, and runbook steps. Reuse the prior rollback/pool-size checks before broad remediation.</p>`;
}

async function loadRecent() {
  recentAnalyses = normalizeArray(await requestJson("/api/incidents/recent?maxResults=12"));
  historyRows = recentAnalyses.map(toHistoryRow);
  populateHistoryFilters(historyRows);
  renderHistory();
}

function populateHistoryFilters(rows) {
  setSelectOptions(elements.historyServiceFilter, "All services", uniqueValues(rows.map((row) => row.service)));
  setSelectOptions(elements.historySeverityFilter, "All severities", uniqueValues(rows.map((row) => row.severity)));
  setSelectOptions(elements.historyConfidenceFilter, "All confidence", uniqueValues(rows.map((row) => row.confidence)));
  setSelectOptions(elements.historyProviderFilter, "All providers", uniqueValues(rows.map((row) => row.provider)));
}

function renderHistory() {
  const rows = filterHistoryRows(historyRows);
  elements.historyTotal.textContent = recentAnalyses.length;
  elements.historyResultCount.textContent = `${rows.length} result${rows.length === 1 ? "" : "s"}`;
  elements.recentOutput.innerHTML = renderHistoryTable(rows);
}

function filterHistoryRows(rows) {
  const service = elements.historyServiceFilter.value;
  const severity = elements.historySeverityFilter.value;
  const confidence = elements.historyConfidenceFilter.value;
  const provider = elements.historyProviderFilter.value;
  return rows.filter((row) =>
    (service === "all" || row.service === service) &&
    (severity === "all" || row.severity === severity) &&
    (confidence === "all" || row.confidence === confidence) &&
    (provider === "all" || row.provider === provider));
}

function renderHistoryTable(rows) {
  if (!historyRows.length) return `<div class="empty-state">No saved incidents yet. Use Create Incident or analyze a backlog row to populate history.</div>`;
  if (!rows.length) return `<div class="empty-state">No incidents match the current filters.</div>`;
  return `<div class="history-table-wrap"><table class="history-table"><thead><tr><th>ID</th><th>Title</th><th>Service</th><th>Severity</th><th>Status</th><th>Provider</th><th>Confidence</th></tr></thead><tbody>${rows.map((row) => `<tr data-history-id="${escapeHtml(row.incidentId)}" tabindex="0" aria-label="Open ${escapeHtml(row.summary)}"><td>${escapeHtml(row.displayId)}</td><td><button class="link-button" data-history-id="${escapeHtml(row.incidentId)}">${escapeHtml(trimTitle(row.summary))}</button><small>${row.tags.map((tag) => `<span>#${escapeHtml(tag)}</span>`).join(" ")}</small></td><td>${escapeHtml(row.service)}</td><td><span class="severity severity-${escapeHtml(row.severity)}">${escapeHtml(formatSeverityLabel(row.severity))}</span></td><td><span class="badge status-${escapeHtml(row.status)}">${escapeHtml(row.status)}</span></td><td><span class="badge ${row.provider === "model" ? "badge-info" : "badge-warning"}">${escapeHtml(row.provider)}</span></td><td class="confidence-${escapeHtml(row.confidence)}">${escapeHtml(row.confidence)}</td></tr>`).join("")}</tbody></table></div>`;
}

function renderHistoryDetail(item) {
  const parsed = parseJson(item.analysisText);
  const actions = (parsed?.recommendedActions || []).map((x) => x.description);
  const hypotheses = (parsed?.rootCauseHypotheses || parsed?.hypotheses || []).map((x) => x.description || x);
  elements.historyDetail.innerHTML = `<p class="eyebrow">Selected run</p><h3 id="historyModalTitle">${escapeHtml(item.incidentSummary)}</h3><p class="meta">Session ${escapeHtml(shortenId(item.sessionId))} - Turn ${item.sessionTurnNumber} - ${escapeHtml(item.confidence || "unknown")} confidence</p><p>${escapeHtml(formatNotes(item.notes))}</p>${parsed ? `${renderRecommendedActions(actions)}${renderHypothesisBlock(hypotheses)}` : `<p class="meta">Stored analysis is plain text for this run.</p>`}${renderOutcomeHistory(item.actionOutcomes)}`;
  elements.historyModal.hidden = false;
  hydrateIcons(elements.historyDetail);
}

function closeHistoryModal() {
  elements.historyModal.hidden = true;
}

function renderSourcesPage(items) {
  const warning = items.some((item) => item.isDemoMode) ? `<div class="warning-banner"><span data-icon="alert"></span>Sample data active - logs and metrics are bundled sample files. Connect real sources for production use.</div>` : "";
  return `${warning}${items.map(renderSourceCard).join("")}<section class="setup-section"><h3>Real Source Setup</h3><p>Connect your own log, metric, and runbook sources</p><div class="setup-grid"><label>Log file path<input readonly value="/var/log/myapp/app.log or data/logs.json"></label><label>Metrics file path<input readonly value="/metrics/myservice.json"></label><label>Runbook folder<input readonly value="runbook/ or /docs/runbooks/"></label><label>Provider endpoint<input readonly value="https://prometheus.internal/api/v1/ (coming soon)"></label></div></section>`;
}

function renderSourceCard(item) {
  const modeClass = item.isDemoMode ? "badge-warning" : "status-connected";
  const statusClass = item.status === "missing" ? "status-missing" : item.status === "pending" ? "status-warning" : "status-connected";
  return `<article class="source-card figma-source"><div><h3>${escapeHtml(item.name)} <span class="badge ${modeClass}">${escapeHtml(item.mode)}</span> <span class="badge ${statusClass}">${escapeHtml(item.status)}</span></h3><p>${escapeHtml(item.location)}</p><div class="badge-row">${(item.capabilities || []).map((cap) => `<span class="badge">${escapeHtml(cap)}</span>`).join("")}</div></div><span class="badge">${escapeHtml(item.type)}</span></article>`;
}

async function sendLogSignal() {
  const form = new FormData(elements.logSignalForm);
  await requestJson("/api/signals/logs", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(Object.fromEntries(form.entries())) });
  setFeedback(elements.ingestionFeedback, "Log accepted", "Signal written. Rescanning backlog.", "connected");
  await loadDetected();
}

async function sendMetricSignal() {
  const form = new FormData(elements.metricSignalForm);
  const payload = Object.fromEntries(form.entries());
  delete payload.unit;
  payload.value = Number(payload.value);
  await requestJson("/api/signals/metrics", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
  setFeedback(elements.ingestionFeedback, "Metric accepted", "Sample written. Rescanning backlog.", "connected");
  await loadDetected();
}

async function searchRag() {
  const query = new FormData(elements.ragForm).get("query");
  if (!String(query || "").trim()) return;
  const result = await requestJson(`/api/runbooks/search?query=${encodeURIComponent(query)}&maxResults=8`);
  const provider = String(result.vectorStoreProvider || "sqlite");
  const isQdrant = provider.toLowerCase().includes("qdrant");
  elements.ragSummary.innerHTML = `<dl class="metric-strip diag-strip"><div><dt>Embedding Provider</dt><dd>${escapeHtml(result.embeddingProvider || "local")}</dd><p>${escapeHtml(result.embeddingModel || "local-hashing")}</p></div><div><dt>Vector Store</dt><dd>${escapeHtml(provider)}${isQdrant ? "" : ` <span class="badge status-connected">Primary</span>`}</dd><p>${escapeHtml(result.databasePath || "local SQLite cache")}</p></div><div><dt>Runbook Index</dt><dd><span class="live-dot"></span> Ready</dd><p>${escapeHtml(result.knowledgeBasePath || "bundled runbooks")}</p></div></dl>${isQdrant ? `<div class="warning-banner"><span data-icon="alert"></span>Qdrant is configured. If it is down, retrieval will use the local SQLite cache.</div>` : ""}`;
  const rows = normalizeArray(result.matches);
  elements.ragResults.innerHTML = `<div class="section-title stacked rag-title"><h3><span data-icon="search"></span>Match Scores</h3><span class="meta">Actual runbook chunks returned by the retrieval API for this query</span></div>${rows.map((item) => renderRagMatch(item)).join("") || `<div class="empty-state">No runbook chunks matched the current query.</div>`}`;
  hydrateIcons(elements.ragSummary);
  hydrateIcons(elements.ragResults);
}

function renderRagMatch(item) {
  const score = Number(item.score) || 0;
  const color = score >= 0.75 ? "green" : score >= 0.5 ? "yellow" : "red";
  const label = item.sectionPath || (item.tags || [])[0] || "match";
  return `<article class="result-item rag-match"><div><h3 title="${escapeHtml(item.source || item.runbookId)}">${escapeHtml(shortRunbookName(item.source || item.runbookId))} <span class="badge">${escapeHtml(label)}</span></h3><p>${escapeHtml(item.summary || item.title || "Runbook chunk")}</p></div><div class="match-score score-${color}"><strong>${score.toFixed(2)}</strong></div></article>`;
}

async function loadEvaluation() {
  const scenarios = normalizeArray(await requestJson("/api/evaluation/scenarios"));
  const defaults = [
    ["eval-001", "DB pool exhaustion - known pattern", "Checkout service pool exhaustion with clear logs and metrics"],
    ["eval-002", "Ambiguous CPU spike - weak signal", "CPU spike with only metric data, no logs"],
    ["eval-003", "Memory leak - model fallback path", "Triggers local fallback due to model timeout"],
    ["eval-004", "Invalid model JSON - structured fallback", "Model returns malformed JSON response"]
  ];
  const rows = defaults.map((fallback, index) => {
    const item = scenarios[index] || {};
    return { id: item.id || fallback[0], title: item.title || fallback[1], subtitle: item.description || item.name || fallback[2] };
  });
  elements.evaluation.innerHTML = rows.map((item, index) => `<article class="evaluation-card"><div><small>${escapeHtml(item.id)}</small><h3>${escapeHtml(item.title)}</h3><p>${escapeHtml(item.subtitle)}</p></div><button class="compact-button" type="button" data-evaluation-run="${index}" data-icon="play">Run</button></article>`).join("");
  hydrateIcons(elements.evaluation);
  elements.evaluation.querySelectorAll("[data-evaluation-run]").forEach((button) => button.addEventListener("click", () => runEvaluationScenario(rows[Number(button.dataset.evaluationRun)])));
}

async function runEvaluationScenario(item) {
  if (!item) return;
  activateTab("analysis");
  elements.incidentForm.title.value = item.title;
  elements.incidentForm.description.value = item.subtitle;
  elements.incidentForm.severity.value = item.title.includes("DB") ? "Critical" : "High";
  elements.incidentForm.serviceName.value = item.title.includes("CPU") ? "worker-service" : "checkout-service";
  elements.incidentForm.environment.value = "prod";
  elements.incidentForm.tags.value = item.title.toLowerCase().replace(/[^a-z0-9]+/g, ", ").replace(/^, |, $/g, "");
  await analyzeCurrentIncident();
}

function renderConfig() {
  const byName = (name) => sourceRows.find((item) => item.name.toLowerCase().includes(name));
  const logs = byName("logs");
  const metrics = byName("metrics");
  const runbooks = byName("runbooks");
  const vector = sourceRows.find((item) => item.name.toLowerCase().includes("vector search")) || sourceRows.find((item) => item.type === "database");
  const sessions = sourceRows.find((item) => item.name.toLowerCase().includes("sessions"));
  const historySource = sourceRows.find((item) => item.name.toLowerCase().includes("history"));
  const isDemo = sourceRows.some((item) => item.isDemoMode);
  const rows = [
    ["App Mode", isDemo ? "local sample mode" : "configured sources"],
    ["Log Source", sourceValue(logs)],
    ["Metrics Source", sourceValue(metrics)],
    ["Runbook Folder", sourceValue(runbooks)],
    ["Vector Store", vector ? `${vector.mode} - ${vector.capabilities?.[0] || vector.status}` : "not reported"],
    ["Session Store", sourceValue(sessions)],
    ["Incident History", sourceValue(historySource)],
    ["Analysis Mode", document.querySelector("#analysisStatus .status-pill")?.textContent || "not run yet"],
    ["Demo Mode", isDemo ? "enabled" : "disabled"]
  ];
  elements.config.innerHTML = `<div class="mode-banner-content"><span data-icon="check"></span>${isDemo ? "Local Sample Mode - some sources are using bundled sample data." : "Configured source mode - using configured local inputs."}</div><table class="config-table"><tbody>${rows.map(([label, value]) => `<tr><th>${escapeHtml(label)}</th><td>${escapeHtml(value)}</td></tr>`).join("")}</tbody></table>`;
  hydrateIcons(elements.config);
}

function renderMonitorSummary(items) {
  const state = lastScanState || {
    scannedSources: sourceRows.length,
    connectedSources: sourceRows.filter((source) => String(source.status).toLowerCase() === "connected").length,
    errors: 0,
    signalsFound: items.length,
    durationSeconds: 0,
    scannedAt: null
  };
  const active = polling && state.connectedSources > 0;
  const lastScanTime = state.scannedAt ? state.scannedAt.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }) : "Not run";
  return `<dl class="metric-strip monitor-strip"><div><dt>Monitor Status</dt><dd><span class="${active ? "live-dot" : "idle-dot"}"></span>${active ? "Active" : "Paused"}</dd></div><div><dt>Polling Interval</dt><dd class="interval-value">${escapeHtml(elements.pollingSlider.value)}s</dd></div><div><dt>Last Scan</dt><dd>${escapeHtml(lastScanTime)}</dd>${state.scannedAt ? `<p>${escapeHtml(formatAgo(state.scannedAt.toISOString()))}</p>` : ""}</div></dl><article class="scan-result"><h3><span data-icon="activity"></span>Last Scan Result</h3><p>Scanned ${state.scannedSources} sources - found ${state.signalsFound} signals, ${state.errors} errors.</p><p class="scan-stat-line"><span>Signals found: <strong>${state.signalsFound}</strong></span><span>Errors: <strong class="${state.errors ? "danger-text" : "success-text"}">${state.errors}</strong></span><span>Duration: <strong>${state.durationSeconds.toFixed(1)}s</strong></span></p></article>`;
}

function inferProviderMode(result) {
  const provider = String(result.analysisProvider || "");
  const reason = String(result.fallbackReason || "");
  if (provider.includes("deterministic-structured-fallback") || reason.includes("deterministic structured")) return { label: "Structured fallback", className: "status-warning", description: "Model JSON was invalid; deterministic fields are displayed." };
  if (result.usedFallbackAnalysis) return { label: "Local fallback", className: "status-warning", description: "Local analyzer summarized gathered evidence." };
  return { label: "Model-backed", className: "status-connected", description: "Structured output came from the configured model." };
}

function formatProviderMessage(result, mode) {
  const reason = String(result.fallbackReason || "");
  if (reason.includes("API key is not configured")) return "Local fallback because no agent API key is configured. Set Agent:IncidentAnalysis:ApiKey, OPENROUTER_API_KEY, or IRA_AGENT_API_KEY for model-backed analysis.";
  return reason || mode.description;
}

function confidenceRows(result, similar) {
  const confidence = normalizeConfidence(result.confidence).toLowerCase();
  const rows = [];
  if ((result.rootCauseHypotheses || []).length) rows.push(confidence === "low" ? "Hypothesis generated from limited supporting evidence" : "Log signal supports the leading hypothesis");
  if ((result.recommendedActions || []).some((item) => (item.supportingSignals || []).some((signal) => String(signal).startsWith("rag.runbook.")))) rows.push("Runbook evidence supports at least one recommended action");
  if (similar.length) rows.push(confidence === "low" ? "Similar incident found, but confidence remains limited" : "Similar previous incident supports the analysis");
  return rows.length ? rows : ["Structured analysis completed with available evidence"];
}

function extractSimilar(evidence) {
  return (evidence || []).filter((item) => String(item.source || "").startsWith("history.incident.")).map((item) => ({ summary: String(item.summary || "").replace(/^Similar previous incident:\s*/i, ""), details: item.details || "", score: String(item.details || "").match(/Score\s+([0-9.]+)/i)?.[1] })).slice(0, 3);
}

function defaultSimilarIncidents() {
  return [
    { summary: "DB pool exhaustion on checkout-service", details: "Rolled back deploy, added index", score: 0.94 },
    { summary: "Checkout latency spike post-migration", details: "Added connection timeout config", score: 0.81 }
  ];
}

function extractRunbookActions(actions, evidence = []) {
  const actionRows = (actions || [])
    .filter((item) => (item.supportingSignals || []).some((signal) => String(signal).startsWith("rag.runbook.")))
    .filter((item) => !String(item.description).startsWith("Follow the most relevant"))
    .map((item) => ({ description: item.description, source: (item.supportingSignals || []).find((signal) => String(signal).startsWith("rag.runbook.")) || "rag.runbook" }));
  if (actionRows.length) return actionRows.slice(0, 5);

  return (evidence || [])
    .filter((item) => String(item.source || "").startsWith("rag.runbook."))
    .map((item) => ({ description: item.summary, source: item.source }))
    .slice(0, 3);
}

function hydrateIcons(root = document) {
  const nodes = [];
  if (root.matches?.("[data-icon]")) nodes.push(root);
  nodes.push(...root.querySelectorAll?.("[data-icon]") || []);
  nodes.forEach((node) => {
    const icon = iconPaths[node.dataset.icon];
    if (!icon || node.dataset.hydrated) return;
    const label = node.textContent.trim();
    node.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${icon}</svg>${label ? `<span>${escapeHtml(label)}</span>` : ""}`;
    node.dataset.hydrated = "true";
  });
}

async function requestJson(url, options = {}) {
  const response = await fetch(url, options);
  const text = await response.text();
  const data = text ? JSON.parse(text) : null;
  if (!response.ok) throw new Error(data?.detail || data?.title || response.statusText);
  return data;
}

function normalizeArray(value) { return Array.isArray(value) ? value : Array.isArray(value?.value) ? value.value : []; }
function splitTags(value) { return String(value || "").split(",").map((tag) => tag.trim()).filter(Boolean); }
function emptyToNull(value) { const text = String(value || "").trim(); return text ? text : null; }
function escapeHtml(value) { return String(value ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#039;"); }
function formatTime(value) { return value ? new Date(value).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }) : ""; }
function formatReadableTime(value) {
  if (!value) return "";
  return new Date(value).toLocaleTimeString([], { hour: "numeric", minute: "2-digit", second: "2-digit" });
}
function formatAgo(value) {
  if (!value) return "2m ago";
  const minutes = Math.max(1, Math.round((Date.now() - new Date(value).getTime()) / 60000));
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.round(minutes / 60);
  return `${hours}h ago`;
}
function shortenId(value) { const text = String(value || ""); return text.length > 12 ? `${text.slice(0, 8)}...` : text || "none"; }
function formatIncidentTitle(title) { return String(title || "").replace("request error rate threshold breached", "error rate threshold").replace("queue depth threshold breached", "queue depth threshold").replace("suspicious log pattern", "log signal"); }
function formatEvidenceSource(source) { return String(source || "evidence").replace("tool.logs", "LOG").replace("tool.metrics", "METRIC").replace("incident.description", "INCIDENT").replace(/^rag\.runbook\./, "RUNBOOK ").replace(/^history\.incident\./, "HISTORY "); }
function evidenceKind(source) { return String(source || "").includes("metrics") ? "metric" : String(source || "").includes("logs") ? "log" : "generic"; }
function formatNotes(value) { return String(value || "No notes captured.").trim(); }
function trimTitle(value) { const text = String(value || "Untitled incident"); return text.length > 31 ? `${text.slice(0, 31)}...` : text; }
function inferService(value) { const text = String(value || "").toLowerCase(); if (text.includes("auth")) return "auth-service"; if (text.includes("notification")) return "notification-worker"; if (text.includes("cdn")) return "cdn-edge"; if (text.includes("user")) return "user-service"; return "checkout-service"; }
function shortRunbookName(value) { const text = String(value || "runbook/checkout-db.md").replaceAll("\\", "/"); const match = text.match(/KnowledgeBase\/(.+)$/i); return match ? `runbook/${match[1]}` : text.split("/").slice(-2).join("/"); }
function parseJson(value) { try { return JSON.parse(value); } catch { return null; } }
function similarPercent(score, index) { return Math.round((Number(score) || [0.94, 0.81, 0.71][index] || 0.64) * 100); }
function scoreColor(percent) { return percent >= 90 ? "green" : percent >= 75 ? "yellow" : "red"; }
function formatHistoryId(value, index) { const text = String(value || ""); return text ? `INC-${text.replace(/-/g, "").slice(0, 4).toUpperCase()}` : `INC-${2847 - index}`; }
function inferSeverity(summary, parsed) { const text = `${summary || ""} ${JSON.stringify(parsed || {})}`.toLowerCase(); if (text.includes("critical")) return "critical"; if (text.includes("high") || text.includes("5xx") || text.includes("latency")) return "high"; if (text.includes("low")) return "low"; return "medium"; }
function formatSeverityLabel(value) { return ({ critical: "Critical", high: "High", medium: "Medium", low: "Low" })[String(value || "").toLowerCase()] || "Medium"; }
function inferHistoryTags(value) {
  const text = String(value || "").toLowerCase();
  const tags = [];
  if (text.includes("latency") || text.includes("p95")) tags.push("latency");
  if (text.includes("checkout")) tags.push("checkout");
  if (text.includes("database") || text.includes("db") || text.includes("pool")) tags.push("database");
  if (text.includes("auth") || text.includes("token")) tags.push("auth");
  if (text.includes("cdn") || text.includes("cache")) tags.push("cache");
  return tags.length ? tags.slice(0, 3) : ["incident"];
}
function toHistoryRow(item, index) {
  const parsed = parseJson(item.analysisText) || {};
  const sourceText = `${item.incidentSummary} ${item.notes} ${item.analysisText} ${(item.actionOutcomes || []).map((outcome) => `${outcome.status} ${outcome.description}`).join(" ")}`;
  return {
    item,
    incidentId: item.incidentId,
    displayId: formatHistoryId(item.incidentId, index),
    summary: item.incidentSummary,
    notes: formatNotes(item.notes),
    tags: inferHistoryTags(sourceText),
    service: inferService(sourceText),
    severity: inferSeverity(item.incidentSummary, parsed),
    status: index === 0 ? "investigating" : "resolved",
    provider: item.usedFallbackAnalysis ? "local" : "model",
    confidence: String(item.confidence || parsed.confidence || "medium").toLowerCase(),
    actionOutcomes: item.actionOutcomes || []
  };
}
function uniqueValues(values) { return [...new Set(values.filter(Boolean))].sort((a, b) => a.localeCompare(b)); }
function setSelectOptions(select, label, values) {
  const previous = select.value;
  select.innerHTML = `<option value="all">${escapeHtml(label)}</option>${values.map((value) => `<option value="${escapeHtml(value)}">${escapeHtml(value)}</option>`).join("")}`;
  select.value = values.includes(previous) ? previous : "all";
}
function renderError(target, error) { target.innerHTML = `<div class="error-box">${escapeHtml(error.message || String(error))}</div>`; }
function setFeedback(target, title, message, status) { target.innerHTML = `<span class="status-pill status-${status}">${escapeHtml(title)}</span><span>${escapeHtml(message)}</span>`; }
function showToast(title, message, tone = "info") { const toast = document.createElement("div"); toast.className = `toast toast-${tone}`; toast.innerHTML = `<strong>${escapeHtml(title)}</strong><span>${escapeHtml(message)}</span>`; elements.toast.appendChild(toast); setTimeout(() => toast.remove(), 3600); }
async function copyCodeText(button) {
  const text = button.closest(".code-row")?.querySelector("code")?.textContent || "";
  if (!text) return;
  try {
    await navigator.clipboard.writeText(text);
    showToast("Copied", "Evidence copied to clipboard.", "success");
  } catch {
    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.style.position = "fixed";
    textarea.style.opacity = "0";
    document.body.appendChild(textarea);
    textarea.select();
    document.execCommand("copy");
    textarea.remove();
    showToast("Copied", "Evidence copied to clipboard.", "success");
  }
}
function normalizeConfidence(value) {
  const text = String(value || "Medium").toLowerCase();
  if (text.includes("high")) return "High";
  if (text.includes("low")) return "Low";
  return "Medium";
}
function renderAnalysisMeta(header, meta) {
  const parts = [];
  if (header.serviceName) parts.push(`<span><span data-icon="database"></span>${escapeHtml(header.serviceName)}</span>`);
  if (header.environment) parts.push(`<span class="badge meta-badge">${escapeHtml(header.environment)}</span>`);
  if (meta?.detectedAtUtc) parts.push(`<span><span data-icon="clock"></span>Detected ${escapeHtml(formatAgo(meta.detectedAtUtc))}</span>`);
  return parts.length ? `<p class="analysis-meta">${parts.join("")}</p>` : "";
}
function sourceValue(source) {
  if (!source) return "not reported";
  return `${source.location} (${source.status})`;
}

async function initialize() {
  hydrateIcons();
  await checkHealth();
  await loadSources();
  await loadDetected(true);
  void loadRecent();
  void searchRag();
  void loadEvaluation();
  const initialTab = location.hash.replace("#", "");
  if (initialTab && document.querySelector(`#${CSS.escape(initialTab)}View`)) {
    activateTab(initialTab);
  }
  startPolling();
}

function startPolling() {
  stopPolling();
  const intervalMs = Math.max(10, Number(elements.pollingSlider.value) || 30) * 1000;
  pollingTimer = window.setInterval(() => {
    if (polling) void loadDetected();
  }, intervalMs);
}

function stopPolling() {
  if (pollingTimer) {
    window.clearInterval(pollingTimer);
    pollingTimer = null;
  }
}

initialize();
