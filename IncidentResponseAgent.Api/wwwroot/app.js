const $ = (selector) => {
  const element = document.querySelector(selector);
  if (!element) throw new Error(`Missing ${selector}`);
  return element;
};

const elements = {
  health: $("#healthStatus"),
  projectSelector: $("#projectSelector"),
  appVersion: $("#appVersion"),
  sidebarLastScan: $("#sidebarLastScan"),
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
  historyReload: $("#historyReloadButton"),
  historySearch: $("#historySearch"),
  historyServiceFilter: $("#historyServiceFilter"),
  historyStatusFilter: $("#historyStatusFilter"),
  historySessionFilter: $("#historySessionFilter"),
  historySeverityFilter: $("#historySeverityFilter"),
  historyTotal: $("#historyTotal"),
  historyResultCount: $("#historyResultCount"),
  recentOutput: $("#recentOutput"),
  historyModal: $("#historyModal"),
  historyModalClose: $("#historyModalClose"),
  historyDetail: $("#historyDetail"),
  confirmModal: $("#confirmModal"),
  confirmModalTitle: $("#confirmModalTitle"),
  confirmModalMessage: $("#confirmModalMessage"),
  confirmModalCancel: $("#confirmModalCancel"),
  confirmModalAccept: $("#confirmModalAccept"),
  projectForm: $("#projectForm"),
  sources: $("#sourcesOutput"),
  ragForm: $("#ragForm"),
  ragSummary: $("#ragSummary"),
  ragResults: $("#ragResults"),
  runbookSourceForm: $("#runbookSourceForm"),
  runbookSources: $("#runbookSources"),
  evaluation: $("#evaluationOutput"),
  logSignalForm: $("#logSignalForm"),
  metricSignalForm: $("#metricSignalForm"),
  ingestionFeedback: $("#ingestionFeedback"),
  config: $("#configOutput"),
  toast: $("#toastRegion"),
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
  link: '<path d="M10 13a5 5 0 0 0 7.5.5l2-2a5 5 0 0 0-7-7l-1.1 1"/><path d="M14 11a5 5 0 0 0-7.5-.5l-2 2a5 5 0 0 0 7 7l1.1-1"/>',
  copy: '<rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>',
  trash: '<path d="M3 6h18"/><path d="M8 6V4h8v2"/><path d="M19 6l-1 14H6L5 6"/><path d="M10 11v5"/><path d="M14 11v5"/>'
  ,"thumbs-up": '<path d="M7 10v12"/><path d="M15 5.88 14 10h5.83a2 2 0 0 1 1.92 2.56l-2.33 8A2 2 0 0 1 17.5 22H4a2 2 0 0 1-2-2v-8a2 2 0 0 1 2-2h2.76a2 2 0 0 0 1.79-1.11L12 2h0a3.13 3.13 0 0 1 3 3.88Z"/>'
  ,"thumbs-down": '<path d="M17 14V2"/><path d="M9 18.12 10 14H4.17a2 2 0 0 1-1.92-2.56l2.33-8A2 2 0 0 1 6.5 2H20a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2h-2.76a2 2 0 0 0-1.79 1.11L12 22h0a3.13 3.13 0 0 1-3-3.88Z"/>'
  ,minus: '<path d="M5 12h14"/>'
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
let analysisWaitTimer = null;
let confirmationResolver = null;
let confirmationReturnFocus = null;
let dashboardPage = 1;
let historyPage = 1;
let projects = [];
let activeProjectId = localStorage.getItem("incidentops.projectId") || "default";
const pageSize = 10;
const incidentLifecycleStatuses = ["new", "active", "mitigated", "resolved", "recovered"];
const severityFilterValues = ["sev1", "sev2", "sev3", "sev4", "sev5"];

document.querySelectorAll(".nav-tab").forEach((button) => button.addEventListener("click", () => activateTab(button.dataset.tab)));
document.querySelectorAll(".filter-chip").forEach((button) => {
  button.addEventListener("click", () => {
    document.querySelectorAll(".filter-chip").forEach((item) => item.classList.toggle("active", item === button));
    activeStatus = button.dataset.status;
    dashboardPage = 1;
    renderDetected();
  });
});

elements.scan.addEventListener("click", toggleTheme);
elements.incidentSearch.addEventListener("input", () => {
  dashboardPage = 1;
  renderDetected();
});
elements.manualIncident.addEventListener("click", showManualIncidentForm);
elements.projectForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  await addProjectFromForm();
});
elements.projectSelector.addEventListener("change", async () => {
  activeProjectId = elements.projectSelector.value || "default";
  localStorage.setItem("incidentops.projectId", activeProjectId);
  dashboardPage = 1;
  historyPage = 1;
  lastScanState = null;
  localStorage.removeItem("incidentops.lastScan");
  await refreshProjectScopedViews();
  showToast("Project changed", activeProjectId === "all" ? "Showing incidents across all projects." : `Scoped to ${projectName(activeProjectId)}.`, "info");
});
elements.sample.addEventListener("click", loadSampleIncident);
elements.historyReload.addEventListener("click", async () => {
  if (elements.historyReload.disabled) return;
  elements.historyReload.disabled = true;
  elements.historyReload.setAttribute("aria-busy", "true");
  elements.historyReload.innerHTML = '<span class="loading-spinner" aria-hidden="true"></span>';
  try {
    await loadRecent();
    showToast("History refreshed", "Saved incidents reloaded.", "success");
  } catch (error) {
    showToast("History refresh failed", error.message || String(error), "error");
  } finally {
    elements.historyReload.disabled = false;
    elements.historyReload.removeAttribute("aria-busy");
    elements.historyReload.innerHTML = '<span data-icon="refresh"></span>';
    hydrateIcons(elements.historyReload);
  }
});
[elements.historyServiceFilter, elements.historyStatusFilter, elements.historySessionFilter, elements.historySeverityFilter].forEach((select) => {
  select.addEventListener("change", () => {
    historyPage = 1;
    renderHistory();
  });
});
elements.historySearch.addEventListener("input", () => {
  historyPage = 1;
  renderHistory();
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
$("#pauseScanButton").addEventListener("click", async () => {
  try {
    const state = await requestJson(`/api/monitoring/${polling ? "pause" : "resume"}`, { method: "POST" });
    applyServerMonitoringState(state);
    showToast(polling ? "Monitoring resumed" : "Monitoring paused", polling ? "Server-side scans continue even when this page is closed." : "The persisted server-side schedule is paused.", "success");
  } catch (error) { showToast("Monitoring update failed", error.message || String(error), "error"); }
});
elements.manualRefresh.addEventListener("click", () => loadDetected(true));
elements.pollingSlider.addEventListener("input", () => {
  syncPollingControl();
  elements.lastScan.innerHTML = renderMonitorSummary(detectedCandidates);
  hydrateIcons(elements.lastScan);
});
elements.pollingSlider.addEventListener("change", () => {
  void updateServerPollingInterval();
});
elements.historyModalClose.addEventListener("click", closeHistoryModal);
elements.historyModal.addEventListener("click", (event) => {
  if (event.target === elements.historyModal) closeHistoryModal();
});
elements.runbookSourceForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  await connectRunbookSource();
});
elements.runbookSources.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-runbook-source-action]");
  if (!button) return;
  await handleRunbookSourceAction(button);
});
elements.sources.addEventListener("click", async (event) => {
  const button = event.target.closest("[data-remove-project]");
  if (!button) return;
  await removeProject(button.dataset.removeProject);
});
elements.confirmModalCancel.addEventListener("click", () => closeConfirmation(false));
elements.confirmModalAccept.addEventListener("click", () => closeConfirmation(true));
elements.confirmModal.addEventListener("click", (event) => {
  if (event.target === elements.confirmModal) closeConfirmation(false);
});
elements.historyDetail.addEventListener("click", async (event) => {
  const deleteButton = event.target.closest("[data-delete-incident]");
  if (deleteButton) {
    await deleteIncident(deleteButton.dataset.deleteIncident);
    return;
  }
  const followUpButton = event.target.closest("[data-follow-up-session]");
  if (followUpButton) {
    clearIncidentForm(followUpButton.dataset.followUpSession);
    closeHistoryModal();
    activateTab("analysis");
    document.querySelector(".analysis-layout").classList.add("show-input");
    elements.incidentForm.title.focus();
    showToast("Session linked", "The next analysis will continue this incident session.", "success");
    return;
  }
  const copySessionButton = event.target.closest("[data-copy-session]");
  if (copySessionButton) {
    await copyText(copySessionButton.dataset.copySession, "Session ID copied");
    return;
  }
  const publishedKnowledgeButton = event.target.closest("[data-view-published-knowledge]");
  if (publishedKnowledgeButton) {
    elements.ragForm.query.value = publishedKnowledgeButton.dataset.viewPublishedKnowledge;
    closeHistoryModal();
    activateTab("rag");
    showToast("Searching published knowledge", "The RAG view shows the indexed approved Markdown and its source path.", "info");
    return;
  }
  const button = event.target.closest("[data-ticket-status]");
  if (button) {
    await updateIncidentStatus(button.dataset.incidentId, button.dataset.ticketStatus);
    return;
  }
  const reviewButton = event.target.closest("[data-knowledge-review]");
  if (reviewButton) await reviewKnowledgeUpdate(reviewButton.dataset.incidentId, reviewButton.dataset.knowledgeReview);
});
document.addEventListener("keydown", (event) => {
  const activeModal = !elements.confirmModal.hidden ? elements.confirmModal : !elements.historyModal.hidden ? elements.historyModal : null;
  if (!activeModal) return;
  if (event.key === "Escape") {
    if (activeModal === elements.confirmModal) closeConfirmation(false); else closeHistoryModal();
    return;
  }
  if (event.key !== "Tab") return;
  const focusable = getModalFocusableElements(activeModal);
  if (!focusable.length) return;
  const first = focusable[0];
  const last = focusable[focusable.length - 1];
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault();
    last.focus();
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault();
    first.focus();
  }
});

let historyModalReturnFocus = null;

function toggleTheme() {
  document.documentElement.dataset.theme = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
  localStorage.setItem("incidentops.theme", document.documentElement.dataset.theme);
}
elements.detected.addEventListener("click", async (event) => {
  const pageButton = event.target.closest("[data-dashboard-page]");
  if (pageButton) {
    dashboardPage = Number(pageButton.dataset.dashboardPage);
    renderDetected();
    return;
  }

  const button = event.target.closest("button[data-action]");
  if (!button) return;
  if (button.dataset.action === "open-ticket") {
    const item = recentAnalyses.find((analysis) => analysis.incidentId === button.dataset.id);
    if (item) renderHistoryDetail(item);
    return;
  }

  const item = detectedCandidates.find((candidate) => candidate.id === button.dataset.id);
  if (!item) return;
  if (button.dataset.action === "confirm") {
    await confirmCandidate(item);
    return;
  }
  if (["false_positive", "ignored", "merged"].includes(button.dataset.action)) {
    await decideCandidate(item, button.dataset.action);
  }
});
elements.recentOutput.addEventListener("click", (event) => {
  const pageButton = event.target.closest("[data-history-page]");
  if (pageButton) {
    historyPage = Number(pageButton.dataset.historyPage);
    renderHistory();
    return;
  }

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
    await loadRecent();
    const incident = recentAnalyses.find((item) => item.incidentId === historyButton.dataset.historyLink);
    if (incident) renderHistoryDetail(incident); else showToast("Incident unavailable", "The linked incident is no longer in history.", "warning");
    return;
  }
  const feedbackButton = event.target.closest("[data-submit-feedback]");
  if (feedbackButton) {
    await submitAnalysisFeedback(feedbackButton.closest(".feedback-card"));
    return;
  }
  const feedbackChoice = event.target.closest("[data-feedback-choice]");
  if (feedbackChoice) {
    const card = feedbackChoice.closest(".feedback-card");
    const field = card?.querySelector(`[data-feedback-${feedbackChoice.dataset.feedbackField}]`);
    if (field) field.value = feedbackChoice.dataset.feedbackValue;
    card?.querySelectorAll(`[data-feedback-choice][data-feedback-field="${feedbackChoice.dataset.feedbackField}"]`).forEach((button) => {
      const selected = button === feedbackChoice;
      button.classList.toggle("selected", selected);
      button.setAttribute("aria-pressed", String(selected));
    });
    return;
  }
  const rateButton = event.target.closest("[data-rate-recommendation]");
  if (rateButton) {
    const field = elements.analysisOutput.querySelector("[data-feedback-recommendation]");
    if (field) { field.value = rateButton.dataset.rateRecommendation; field.scrollIntoView({ behavior: "smooth", block: "center" }); field.focus(); }
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
    showToast("Outcome saved", "This outcome becomes reusable only after resolution and knowledge approval.", "success");
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
  if (tab === "rag") { void loadRunbookSources(); void searchRag(); }
  if (tab === "evaluation") void loadEvaluation();
  if (tab === "config") renderConfig();
}

function showManualIncidentForm() {
  clearIncidentForm();
  activateTab("analysis");
  document.querySelector(".analysis-layout").classList.add("show-input");
  activeIncidentMeta = { detectedAtUtc: new Date().toISOString() };
  elements.analysisStatus.textContent = "Create a manual incident, then run analysis.";
  elements.incidentForm.title.focus();
}

function clearIncidentForm(sessionId = "") {
  elements.incidentForm.reset();
  elements.incidentForm.title.value = "";
  elements.incidentForm.description.value = "";
  elements.incidentForm.severity.value = "";
  elements.incidentForm.serviceName.value = "";
  elements.incidentForm.environment.value = "";
  elements.incidentForm.tags.value = "";
  elements.incidentForm.sessionId.value = sessionId;
  activeIncidentMeta = null;
}

async function checkHealth() {
  try {
    const result = await requestJson("/health");
    elements.health.textContent = result.status;
    elements.health.className = "status-pill status-connected";
    elements.appVersion.textContent = result.version ? `v${result.version}` : "v1.0.0";
  } catch {
    elements.health.textContent = "API down";
    elements.health.className = "status-pill status-missing";
    elements.appVersion.textContent = "version unavailable";
  }
}

async function loadProjects() {
  try {
    projects = normalizeArray(await requestJson("/api/projects"));
  } catch {
    projects = [{ id: "default", name: "Default project" }];
  }
  if (!projects.some((project) => project.id === activeProjectId) && activeProjectId !== "all") {
    activeProjectId = projects[0]?.id || "default";
  }
  elements.projectSelector.innerHTML = `<option value="all">Global view</option>${projects.map((project) => `<option value="${escapeHtml(project.id)}">${escapeHtml(project.name || project.id)}</option>`).join("")}`;
  elements.projectSelector.value = activeProjectId;
}

async function refreshProjectScopedViews() {
  await loadProjects();
  await Promise.allSettled([loadSources(), loadPersistedMonitoringState(), loadRecent(), loadDetected(false, false)]);
  elements.lastScan.innerHTML = renderMonitorSummary(detectedCandidates);
  renderSidebarLastScan();
  hydrateIcons(elements.lastScan);
}

async function addProjectFromForm() {
  const form = new FormData(elements.projectForm);
  const payload = Object.fromEntries(form.entries());
  for (const key of ["highErrorRateThreshold", "latencyWarningThresholdMs"]) {
    if (payload[key] === "") delete payload[key];
    else payload[key] = Number(payload[key]);
  }
  try {
    const project = await requestJson("/api/projects", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
    elements.projectForm.reset();
    activeProjectId = project.id;
    localStorage.setItem("incidentops.projectId", activeProjectId);
    await refreshProjectScopedViews();
    showToast("Project added", `${project.name} is now available for monitoring.`, "success");
  } catch (error) {
    showToast("Project not added", error.message || String(error), "error");
  }
}

async function removeProject(projectId) {
  const confirmed = await showConfirmation({ title: "Remove project?", message: "This removes the workspace configuration from this app. It does not delete log files, metric files, or saved incidents.", confirmLabel: "Remove project", destructive: true });
  if (!confirmed) return;
  try {
    await requestJson(`/api/projects/${encodeURIComponent(projectId)}`, { method: "DELETE" });
    if (activeProjectId === projectId) {
      activeProjectId = "all";
      localStorage.setItem("incidentops.projectId", activeProjectId);
    }
    await refreshProjectScopedViews();
    showToast("Project removed", "The workspace configuration was removed.", "success");
  } catch (error) {
    showToast("Project remove failed", error.message || String(error), "error");
  }
}

function projectQuery(prefix = "?") {
  return activeProjectId && activeProjectId !== "all" ? `${prefix}projectId=${encodeURIComponent(activeProjectId)}` : "";
}

function projectName(projectId) {
  return projects.find((project) => project.id === projectId)?.name || projectId || "Default project";
}

function projectBadge(projectId) {
  if (!projectId || activeProjectId !== "all") return "";
  return `<span class="badge project-badge">${escapeHtml(projectName(projectId))}</span>`;
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
  const sampleSources = sourceRows.filter((source) => source.isDemoMode).map((source) => source.name);
  elements.demoPill.hidden = !isDemo;
  elements.demoPill.textContent = "Demo Mode";
  elements.sourceBanner.innerHTML = `
    <div class="mode-banner-content">
      <span data-icon="check"></span>
      <span>${isDemo ? `Sample data active for: ${escapeHtml(sampleSources.join(", "))}. Other sources retain their reported status below.` : "Configured source mode - no source reports bundled sample data."}</span>
    </div>
  `;
  hydrateIcons(elements.sourceBanner);
}

async function loadDetected(userInitiated = false, performScan = userInitiated) {
  if (userInitiated) {
    setFeedback(elements.scanFeedback, "Scanning", "Checking logs and metrics now.", "pending");
    elements.manualRefresh.disabled = true;
    elements.lastScan.innerHTML = renderLoadingState("Scanning sources...");
    hydrateIcons(elements.lastScan);
  }
  try {
    const result = performScan
      ? await requestJson(`/api/monitoring/scan${projectQuery()}`, { method: "POST" })
      : await requestJson(`/api/incidents/detected${projectQuery()}`);
    if (performScan) {
      applyServerMonitoringState(result);
      detectedCandidates = normalizeArray(await requestJson(`/api/incidents/detected${projectQuery()}`));
    } else {
      detectedCandidates = normalizeArray(result?.candidates ?? result);
    }
    if (performScan) {
      const scan = result?.lastScan || {};
      lastScanState = {
        scannedSources: Number(scan.scannedSourceCount) || 0,
        connectedSources: Math.max(0, (Number(scan.scannedSourceCount) || 0) - (Number(scan.errorCount) || 0)),
        errors: Number(scan.errorCount) || 0,
        signalsFound: Number(scan.candidateCount) || 0,
        durationSeconds: Math.max(0, (Number(scan.durationMilliseconds) || 0) / 1000),
        scannedAt: new Date(scan.completedAtUtc)
      };
      saveLastScanState();
    }
  } catch (error) {
    elements.lastScan.innerHTML = renderMonitorSummary(detectedCandidates);
    renderSidebarLastScan();
    if (userInitiated) setFeedback(elements.scanFeedback, "Scan failed", error.message || String(error), "missing");
    hydrateIcons(elements.lastScan);
    elements.manualRefresh.disabled = false;
    throw error;
  }
  elements.lastScan.innerHTML = renderMonitorSummary(detectedCandidates);
  renderSidebarLastScan();
  hydrateIcons(elements.lastScan);
  updateCounts();
  renderDetected();
  if (userInitiated) setFeedback(elements.scanFeedback, "Last scan result", `Scanned ${lastScanState.scannedSources} sources - found ${lastScanState.signalsFound} signals, ${lastScanState.errors} errors.`, lastScanState.errors ? "warning" : "connected");
  elements.manualRefresh.disabled = false;
}

function saveLastScanState() {
  if (!lastScanState?.scannedAt) return;
  localStorage.setItem("incidentops.lastScan", JSON.stringify({
    ...lastScanState,
    scannedAt: lastScanState.scannedAt.toISOString()
  }));
}

function restoreLastScanState() {
  try {
    const stored = JSON.parse(localStorage.getItem("incidentops.lastScan") || "null");
    if (!stored?.scannedAt) return;
    const scannedAt = new Date(stored.scannedAt);
    if (!Number.isFinite(scannedAt.getTime())) return;
    lastScanState = { ...stored, scannedAt };
  } catch {
    localStorage.removeItem("incidentops.lastScan");
  }
}

function renderDetected() {
  const query = elements.incidentSearch.value.trim().toLowerCase();
  const rows = buildDashboardRows()
    .filter((item) => activeStatus === "all" ? item.statusKey !== "resolved" : item.statusKey === activeStatus)
    .filter((item) => !query || [item.title, item.serviceName, item.environment, item.source, item.provider, ...(item.signals || []), ...(item.tags || [])].join(" ").toLowerCase().includes(query));
  const page = paginateRows(rows, dashboardPage);
  dashboardPage = page.page;
  elements.detected.innerHTML = renderBacklogRows(page.items) || `<div class="empty-state">No incidents match the current filter.</div>`;
  elements.detected.insertAdjacentHTML("beforeend", renderPagination("dashboard", page, "data-dashboard-page"));
  hydrateIcons(elements.detected);
}

function renderBacklogRows(items) {
  if (activeProjectId !== "all") return items.map(renderBacklogRow).join("");
  const groups = items.reduce((map, item) => {
    const key = item.projectId || "default";
    if (!map.has(key)) map.set(key, []);
    map.get(key).push(item);
    return map;
  }, new Map());
  return [...groups.entries()].map(([projectId, rows]) => `<section class="project-backlog-group"><h3>${escapeHtml(projectName(projectId))}<span>${rows.length}</span></h3>${rows.map(renderBacklogRow).join("")}</section>`).join("");
}

function buildDashboardRows() {
  const tickets = historyRows.map((row) => ({
    id: row.incidentId,
    rowKind: "ticket",
    incidentNumber: row.displayId,
    title: row.summary,
    severity: formatSeverityLabel(row.severity),
    serviceName: row.service,
    environment: row.environment,
    detectedAtUtc: row.createdAtUtc,
    statusKey: row.status,
    statusLabel: formatStatusLabel(row.status),
    confidence: row.confidence,
    provider: row.provider,
    projectId: row.projectId,
    tags: row.tags
  }));
  const ticketTitles = new Set(tickets.map((ticket) => normalizeAction(ticket.title)));
  const signals = detectedCandidates
    .map(enrichCandidate)
    .filter((signal) => signal.statusKey === "candidate")
    .filter((signal) => !ticketTitles.has(normalizeAction(signal.title)));
  return [...tickets, ...signals];
}

function enrichCandidate(item, index) {
  return {
    ...item,
    rowKind: "signal",
    incidentNumber: `INC-${String(2487 - index).padStart(4, "0")}`,
    statusKey: item.status || "candidate",
    statusLabel: item.status === "candidate" ? "Candidate" : formatStatusLabel(item.status),
    confidence: "low",
    provider: "rule",
    projectId: item.projectId || "default"
  };
}

function renderBacklogRow(item) {
  const action = item.rowKind === "ticket" ? "open-ticket" : "confirm";
  const confidence = normalizeConfidence(item.confidence).toLowerCase();
  const severityKey = String(item.severity || "sev3").toLowerCase().replace("-", "");
  return `
    <article class="backlog-row" data-row-id="${escapeHtml(item.id)}" data-row-kind="${escapeHtml(item.rowKind)}">
      <div>
        <div class="badge-row">
          <span class="badge muted">${escapeHtml(item.incidentNumber)}</span>
          <span class="severity severity-${escapeHtml(severityKey)}">${escapeHtml(formatSeverityLabel(item.severity))}</span>
          <span class="badge status-${escapeHtml(item.statusKey)}">${escapeHtml(item.statusLabel)}</span>
          <span class="badge badge-info">${escapeHtml(item.provider)}</span>
          ${projectBadge(item.projectId)}
        </div>
        <h3>${escapeHtml(formatIncidentTitle(item.title))}</h3>
        <p><span>${escapeHtml(item.serviceName || "unknown")}</span><span class="badge meta-badge">${escapeHtml(item.environment || "unknown")}</span><span class="conf-label confidence-${escapeHtml(confidence)}">${escapeHtml(confidence)} conf.</span></p>
        ${item.rowKind === "signal" ? `<p class="candidate-evidence"><strong>Evidence:</strong> ${escapeHtml((item.signals || []).slice(0, 2).join(" · ") || "No observable signal supplied")}</p>` : ""}
      </div>
      <div class="row-side">
        ${item.rowKind === "signal" && item.statusKey === "candidate" ? `<div class="candidate-actions"><button type="button" data-action="confirm" data-id="${escapeHtml(item.id)}">Confirm</button><button class="secondary" type="button" data-action="false_positive" data-id="${escapeHtml(item.id)}">False positive</button><button class="secondary" type="button" data-action="ignored" data-id="${escapeHtml(item.id)}">Ignore</button>${item.duplicateIncidentId ? `<button class="secondary" type="button" data-action="merged" data-id="${escapeHtml(item.id)}">Merge duplicate</button>` : ""}</div>` : `<button class="icon-row-button" type="button" data-action="${escapeHtml(action)}" data-id="${escapeHtml(item.id)}">&rsaquo;</button>`}
        <span class="time-ago"><span data-icon="clock"></span>${escapeHtml(formatAgo(item.detectedAtUtc))}</span>
      </div>
    </article>
  `;
}

function updateCounts() {
  const rows = buildDashboardRows();
  $("#newCount").textContent = rows.filter((item) => item.statusKey === "candidate").length;
  $("#confirmedCount").textContent = rows.filter((item) => item.statusKey === "new").length;
  $("#investigatingCount").textContent = rows.filter((item) => item.statusKey === "active").length;
  $("#mitigatedCount").textContent = rows.filter((item) => item.statusKey === "mitigated").length;
  $("#resolvedCount").textContent = rows.filter((item) => item.statusKey === "resolved").length;
}

function loadSampleIncident() {
  document.querySelector(".analysis-layout").classList.add("show-input");
  fillIncidentForm({
    title: "Orders worker queue backlog growth",
    description: "Order fulfillment jobs are piling up faster than workers can process them. Customers are seeing delayed confirmations after checkout.",
    severity: "sev3",
    serviceName: "orders-worker",
    environment: "prod",
    detectedAtUtc: new Date().toISOString(),
    suggestedTags: ["queue", "backlog", "orders"]
  });
}

function fillIncidentForm(item) {
  activeIncidentMeta = item;
  elements.incidentForm.title.value = formatIncidentTitle(item.title || "");
  elements.incidentForm.description.value = item.description || "";
  elements.incidentForm.severity.value = String(item.severity || "sev3").toLowerCase();
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
  startAnalysisWait("Gathering evidence and contacting the configured model...");
  elements.analysisOutput.className = "empty-state";
  elements.analysisOutput.innerHTML = renderLoadingState("Analyzing incident...");
  const form = new FormData(elements.incidentForm);
  const payload = {
    title: form.get("title"),
    description: form.get("description"),
    severity: form.get("severity"),
    serviceName: emptyToNull(form.get("serviceName")),
    environment: emptyToNull(form.get("environment")),
    timestamp: activeIncidentMeta?.detectedAtUtc || currentAnalysisAt,
    sessionId: emptyToNull(form.get("sessionId")),
    tags: splitTags(form.get("tags")),
    projectId: activeProjectId === "all" ? "default" : activeProjectId
  };
  try {
    const candidate = await requestJson("/api/incidents/candidates/manual", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
    const sessionQuery = payload.sessionId ? `?sessionId=${encodeURIComponent(payload.sessionId)}` : "";
    const result = await requestJson(`/api/incidents/candidates/${encodeURIComponent(candidate.id)}/confirm${sessionQuery}`, { method: "POST" });
    currentIncidentId = result.incidentId;
    elements.incidentForm.sessionId.value = result.sessionId;
    renderAnalysis(result);
    void loadRecent();
    showToast("Incident confirmed", `${inferProviderMode(result).label}, turn ${result.sessionTurnNumber}.`, result.usedFallbackAnalysis ? "warning" : "success");
  } catch (error) {
    renderError(elements.analysisOutput, error);
    elements.analysisStatus.textContent = "Analysis failed.";
    showToast("Analysis failed", error.message || String(error), "error");
  } finally {
    stopAnalysisWait();
    elements.analyze.disabled = false;
  }
}

async function confirmCandidate(item) {
  fillIncidentForm(item);
  elements.incidentForm.sessionId.value = "";
  activateTab("analysis");
  startAnalysisWait("Confirming candidate, gathering evidence, and contacting the model...");
  elements.analysisOutput.innerHTML = renderLoadingState("Analyzing confirmed incident...");
  try {
    const result = await requestJson(`/api/incidents/candidates/${encodeURIComponent(item.id)}/confirm`, { method: "POST" });
    currentIncidentId = result.incidentId;
    renderAnalysis(result);
    await Promise.all([loadRecent(), loadDetected(false)]);
    showToast("Candidate confirmed", "The incident is active and its evidence-grounded analysis is ready.", "success");
  } catch (error) {
    renderError(elements.analysisOutput, error);
    elements.analysisStatus.textContent = "Analysis failed.";
    showToast("Analysis failed", error.message || String(error), "error");
  } finally { stopAnalysisWait(); }
}

async function decideCandidate(item, decision) {
  const label = decision.replace("_", " ");
  const confirmed = await showConfirmation({
    title: `${label[0].toUpperCase()}${label.slice(1)} candidate?`,
    message: `This will mark “${item.title}” as ${label}. The decision is recorded in its timeline.`,
    confirmLabel: label === "false positive" ? "Mark false positive" : label === "merged" ? "Merge candidate" : "Ignore candidate",
    destructive: decision !== "merged"
  });
  if (!confirmed) return;
  const mergeIntoIncidentId = decision === "merged" ? item.duplicateIncidentId : null;
  try {
    await requestJson(`/api/incidents/candidates/${encodeURIComponent(item.id)}/decision`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ decision, mergeIntoIncidentId }) });
    await loadDetected(false);
    showToast("Candidate updated", `Candidate marked ${label}.`, "success");
  } catch (error) { showToast("Decision failed", error.message || String(error), "error"); }
}

function renderAnalysis(result) {
  const mode = inferProviderMode(result);
  const similar = result.similarIncidents || [];
  window.currentSimilarIncidents = similar;
  const form = new FormData(elements.incidentForm);
  const header = {
    severity: String(form.get("severity") || "sev3"),
    title: String(form.get("title") || result.incidentSummary),
    description: String(form.get("description") || "No description provided."),
    serviceName: String(form.get("serviceName") || "checkout-service"),
    environment: String(form.get("environment") || ""),
    tags: splitTags(form.get("tags"))
  };
  const confidence = normalizeConfidence(result.confidence);
  const provider = result.providerTransparency || {};
  console.info("Final analysis provider displayed", { provider: provider.modelProvider || result.analysisProvider, model: provider.model || result.analysisModel, fallback: Boolean(provider.usedModelFallback), ragStatus: provider.ragStatus || "unknown", ragDegraded: Boolean(provider.isDegraded), structuredRetry: Boolean(provider.usedStructuredOutputRetry) });
  elements.analysisStatus.innerHTML = `<span class="status-pill ${mode.className}">${escapeHtml(mode.label)}</span><span>${escapeHtml(formatProviderMessage(result, mode))}</span>`;
  elements.analysisOutput.className = "analysis-stack";
  elements.analysisOutput.innerHTML = `
    <article class="analysis-card incident-heading">
      <div class="analysis-title-row">
        <div>
          <div class="badge-row">
            <span class="severity severity-${escapeHtml(header.severity.toLowerCase())}">${escapeHtml(formatSeverityLabel(header.severity))}</span>
            <span class="badge status-investigating">Investigating</span>
            <span class="badge badge-info">${result.sessionTurnNumber > 1 ? `Follow-up ${result.sessionTurnNumber - 1}` : "Original"}</span>
            <span class="badge ${mode.badgeClass}">${escapeHtml(mode.label)}</span>
          </div>
          <h2>${escapeHtml(header.title)}</h2>
          <p>${escapeHtml(header.description)}</p>
          ${renderAnalysisMeta(header, activeIncidentMeta)}
          <div class="tag-row">${header.tags.slice(0, 4).map((tag) => `<span class="badge">#${escapeHtml(tag)}</span>`).join("")}</div>
        </div>
        <div class="confidence-score confidence-${escapeHtml(confidence.toLowerCase())}"><strong>${escapeHtml(confidence)}</strong><span>Confidence</span></div>
      </div>
    </article>
    ${renderProviderTransparency(provider)}
    ${renderRecommendedActions(result.recommendedActions)}
    ${renderGroundedFacts(result.knownFacts)}
    ${renderHypotheses(result.rootCauseHypotheses)}
    ${renderAnalysisBlock("Unknowns & validation gaps", result.unknowns, "info", "unknowns-card")}
    ${renderEvidenceBlock(result.retrievedEvidence)}
    ${renderRunbookMatches(result.runbookMatches)}
    ${renderConfidenceBlock(confidenceRows(result, similar))}
    ${renderAnalysisQuality(result.quality)}
    ${renderSimilarBlock(similar)}
    ${renderPriorActions(similar)}
    ${renderActionOutcomeBlock(result.actionOutcomes)}
    ${renderFeedbackCard()}
  `;
  hydrateIcons(elements.analysisOutput);
}

function renderAnalysisBlock(title, rows, icon, className = "") {
  return `<section class="analysis-card ${escapeHtml(className)}"><h3><span data-icon="${icon}"></span>${escapeHtml(title)}</h3><ul>${(rows || []).slice(0, 6).map((row) => `<li>${escapeHtml(row)}</li>`).join("") || "<li>No data returned.</li>"}</ul></section>`;
}

function renderConfidenceBlock(rows) {
  return `<section class="analysis-card confidence-card"><h3><span data-icon="info"></span>Confidence explanation</h3><div class="confidence-lines">${(rows || []).slice(0, 5).map((row) => `<p><span class="check-dot" data-icon="check"></span>${escapeHtml(row)}</p>`).join("") || `<p><span class="check-dot" data-icon="check"></span>Structured analysis completed with available evidence</p>`}</div></section>`;
}

function renderHypotheses(rows) {
  return `<section class="analysis-card hypothesis-card"><h3><span data-icon="brain"></span>Hypotheses</h3>${(rows || []).slice(0, 5).map((row) => `<div class="grounded-item"><p>${escapeHtml(row.description)}</p><small>${escapeHtml(row.inferenceStrength || "Unknown")} inference · ${escapeHtml(row.confidence || "Low")} confidence · Evidence: ${escapeHtml((row.evidenceReferences || []).join(", "))}</small></div>`).join("") || "<p>No grounded hypothesis is available.</p>"}</section>`;
}

function renderHypothesisBlock(rows) { return renderHypotheses((rows || []).map((description) => ({ description }))); }

function renderRecommendedActions(rows, allowRating = true) {
  return `<section class="analysis-card recommended-card"><h3><span data-icon="wand"></span>Recommended actions</h3><div class="action-lines">${(rows || []).slice(0, 7).map((row) => renderActionLine(row, allowRating)).join("") || "<p>No recommended actions returned.</p>"}</div></section>`;
}

function renderActionLine(row, allowRating = true) {
  const action = typeof row === "string" ? { description: row } : row;
  return `<div class="action-line"><span data-icon="arrow"></span><div><p>${escapeHtml(action.description)}</p>${action.rationale ? `<small>${escapeHtml(action.rationale)}</small>` : ""}${action.supportingSignals?.length ? `<small>Evidence: ${escapeHtml(action.supportingSignals.join(", "))}</small>` : ""}${allowRating ? `<button class="link-inline" type="button" data-rate-recommendation="${escapeHtml(action.description)}">Rate this recommendation</button>` : ""}</div></div>`;
}

function renderGroundedFacts(facts = []) {
  return `<section class="analysis-card facts-card"><h3><span data-icon="check"></span>Verified operational facts</h3>${facts.map((item) => `<div class="grounded-item"><p>${escapeHtml(item.claim)}</p><small>Evidence: ${escapeHtml((item.evidenceReferences || []).join(", "))}</small></div>`).join("") || `<p>No verified log or metric facts were found. The incident description above remains user-reported context.</p>`}</section>`;
}

function renderRunbookMatches(matches = []) {
  return `<section class="analysis-card"><h3><span data-icon="book"></span>Runbook matches (${matches.length})</h3>${matches.map((item) => `<div class="grounded-item"><strong>${escapeHtml(item.title)}</strong><p>${escapeHtml(item.summary)}</p><small>${escapeHtml(item.id)}</small></div>`).join("") || `<p>No runbook matched. This is not evidence that no runbook exists.</p>`}</section>`;
}

function renderAnalysisQuality(quality = {}) {
  const missing = quality.missingData || [];
  return `<section class="analysis-card quality-card"><h3><span data-icon="check"></span>Analysis quality</h3><dl class="quality-grid"><div><dt>Evidence coverage</dt><dd>${escapeHtml(quality.evidenceCoverage || "Low")}</dd></div><div><dt>Runbook match</dt><dd>${escapeHtml(quality.runbookMatchQuality || "Low")}</dd></div><div><dt>Recommendation specificity</dt><dd>${escapeHtml(quality.recommendationSpecificity || "Low")}</dd></div><div><dt>Provider used</dt><dd>${escapeHtml(quality.providerUsed || "unknown")}</dd></div><div><dt>Fallback status</dt><dd>${escapeHtml(quality.fallbackStatus || "not used")}</dd></div></dl><h4>Missing data</h4><ul>${missing.map((item) => `<li>${escapeHtml(item)}</li>`).join("") || "<li>No known missing data was identified.</li>"}</ul></section>`;
}

function renderProviderTransparency(provider = {}) {
  const degradedReason = cleanStatusMessage(provider.degradedReason);
  const fallbackReason = cleanStatusMessage(provider.fallbackReason);
  const retryReason = cleanStatusMessage(provider.structuredOutputRetryReason);
  const modelWarning = cleanStatusMessage(provider.modelResponseWarning);
  const modelTimedOut = /timed out|timeout/i.test(fallbackReason);
  const ragDegraded = Boolean(provider.isDegraded);
  const embeddingDegraded = /embedding/i.test(degradedReason);
  const modelState = provider.usedModelFallback ? (modelTimedOut ? "Timed out" : "Failed") : "Completed";
  const attemptedProvider = provider.attemptedModelProvider || (provider.usedModelFallback && /openrouter/i.test(fallbackReason) ? "OpenRouter" : provider.modelProvider) || "unknown";
  const attemptedModel = provider.attemptedModel || (provider.usedModelFallback && provider.model === "local" ? "configured model" : provider.model) || "model not reported";
  const modelTone = provider.usedModelFallback ? "error" : "ok";
  const embeddingName = embeddingDegraded ? "Local embeddings" : (provider.embeddingProvider || "unknown");
  const embeddingState = embeddingDegraded ? "Primary provider unavailable" : "Available";
  const ragState = ragDegraded ? "Degraded" : String(provider.ragStatus || "unknown");
  return `<section class="analysis-card provider-card"><h3><span data-icon="database"></span>Provider status</h3><div class="provider-status-list">
    ${renderProviderStatus("Model provider", attemptedProvider, `${attemptedModel} · ${modelState}`, modelTone)}
    ${renderProviderStatus("Embedding provider", embeddingName, embeddingState, embeddingDegraded ? "warning" : "ok")}
    ${renderProviderStatus("RAG retrieval", provider.vectorStore || "unknown", ragDegraded ? "Available with degraded embeddings" : ragState, ragDegraded || provider.ragStatus !== "available" ? "warning" : "ok")}
    ${renderProviderStatus("Analysis fallback", provider.usedModelFallback ? "Used" : "Not used", provider.usedModelFallback ? "Local evidence analyzer produced this result" : "The model produced this result", provider.usedModelFallback ? "warning" : "ok")}
  </div>
  <div class="provider-notices">
    ${degradedReason ? `<p class="system-notice notice-warning"><strong>RAG degraded</strong><span>${escapeHtml(degradedReason)}</span></p>` : ""}
    ${fallbackReason ? `<p class="system-notice notice-error"><strong>${modelTimedOut ? "Model timeout" : "Model fallback"}</strong><span>${escapeHtml(fallbackReason)}</span></p>` : ""}
    ${retryReason ? `<p class="system-notice notice-info"><strong>Structured-output retry</strong><span>${escapeHtml(retryReason)}</span></p>` : ""}
    ${modelWarning ? `<p class="system-notice notice-info"><strong>Model response adjusted</strong><span>${escapeHtml(modelWarning)}</span></p>` : ""}
  </div>${renderProviderDiagnostics(provider)}</section>`;
}

function renderProviderDiagnostics(provider = {}) {
  const rows = [
    ["Evidence gathering", provider.evidenceGatheringDurationMilliseconds],
    ["RAG retrieval", provider.ragDurationMilliseconds],
    ["Model execution", provider.modelDurationMilliseconds]
  ].filter(([, value]) => Number(value) > 0);
  const stage = provider.fallbackStage ? formatDiagnosticValue(provider.fallbackStage) : "not triggered";
  const timeoutSource = provider.timeoutSource || "none";
  if (!rows.length && stage === "not triggered" && timeoutSource === "none") return "";
  return `<details class="provider-diagnostics"><summary>Execution diagnostics</summary><dl>${rows.map(([label, value]) => `<div><dt>${escapeHtml(label)}</dt><dd>${escapeHtml(`${Number(value)} ms`)}</dd></div>`).join("")}<div><dt>Fallback stage</dt><dd>${escapeHtml(stage)}</dd></div><div><dt>Timeout source</dt><dd>${escapeHtml(timeoutSource)}</dd></div></dl></details>`;
}

function formatDiagnosticValue(value) { return String(value || "").replaceAll("_", " "); }

function renderProviderStatus(label, value, detail, tone) {
  return `<div class="provider-status-row"><span class="provider-status-dot tone-${escapeHtml(tone)}" aria-hidden="true"></span><div><span class="provider-label">${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong><small>${escapeHtml(detail)}</small></div></div>`;
}

function renderEvidenceBlock(evidence) {
  const visible = (evidence || []).slice(0, 7);
  return `<section class="analysis-card evidence-card"><h3><span data-icon="activity"></span>Evidence (${visible.length})</h3>${visible.map((item) => {
    const evidenceTime = formatEvidenceTime(item.details);
    return `<div class="evidence-line"><div class="evidence-meta"><span class="evidence-label evidence-${escapeHtml(evidenceKind(item.source))}">${escapeHtml(formatEvidenceSource(item.source))}</span>${evidenceTime ? `<time>${escapeHtml(evidenceTime)}</time>` : ""}</div><div class="code-row"><code>${escapeHtml(item.summary)}</code><button type="button" class="copy-code-button" data-copy-code aria-label="Copy evidence"><span data-icon="copy"></span></button></div></div>`;
  }).join("") || `<p class="meta">No operational evidence was returned. Incident metadata is intentionally excluded.</p>`}</section>`;
}

function renderRunbookSteps(actions) {
  if (actions.length === 0) return "";
  return `<section class="analysis-card"><h3><span data-icon="book"></span>Runbook-derived steps (${actions.length})</h3>${actions.map((item, index) => `<div class="step-line"><span>${index + 1}</span><div><strong>${escapeHtml(item.description)}</strong><small>Source: ${escapeHtml(item.source)}</small></div></div>`).join("")}</section>`;
}

function renderSimilarBlock(items) {
  const visible = items || [];
  if (!visible.length) return "";
  return `<section class="analysis-card similar-card"><h3><span data-icon="history"></span>Similar previous incidents (${visible.length})</h3>${visible.map((item) => {
    const id = item.incidentId;
    const percent = similarPercent(item.score, 0);
    const scoreClass = scoreColor(percent);
    return `<div class="similar-line" data-similar-id="${escapeHtml(id)}"><div class="similar-body"><small>${escapeHtml(shortenId(id))} <span class="badge env-badge">${escapeHtml(item.environment || "unknown")}</span> ${escapeHtml(new Date(item.createdAtUtc).toLocaleDateString())}</small><strong>${escapeHtml(item.incidentSummary)}</strong><small>${escapeHtml(item.serviceName || "unknown service")} · Shared signals: ${escapeHtml((item.sharedSignals || []).join(", ") || "none reported")}</small></div><div class="similar-score score-${scoreClass}" title="Weighted lexical similarity plus matching service, environment, and severity"><small>Heuristic match</small><strong>${percent}%</strong><div class="similar-links"><button class="link-inline" type="button" data-history-link="${escapeHtml(id)}">History</button><span aria-hidden="true">&middot;</span><button class="link-inline" type="button" data-compare-incident="${escapeHtml(id)}">Compare</button></div></div><div class="score-bar score-${scoreClass}"><span style="width:${percent}%"></span></div></div>`;
  }).join("")}<div id="comparePanel" class="compare-panel" hidden></div></section>`;
}

function renderPriorActions(items = []) {
  const successful = items.flatMap((item) => (item.successfulActions || []).map((action) => ({ action, incidentId: item.incidentId })));
  const failed = items.flatMap((item) => (item.failedActions || []).map((action) => ({ action, incidentId: item.incidentId })));
  if (!successful.length && !failed.length) return "";
  return `<section class="analysis-card prior-actions-card"><h3><span data-icon="history"></span>Prior action outcomes</h3><div class="prior-action-grid"><div><h4>Successful</h4>${successful.map((item) => `<p><span class="check-dot" data-icon="check"></span>${escapeHtml(item.action)} <small>${escapeHtml(shortenId(item.incidentId))}</small></p>`).join("") || `<p class="meta">None approved.</p>`}</div><div><h4>Failed — do not repeat blindly</h4>${failed.map((item) => `<p class="danger-text">${escapeHtml(item.action)} <small>${escapeHtml(shortenId(item.incidentId))}</small></p>`).join("") || `<p class="meta">None recorded.</p>`}</div></div></section>`;
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

function renderFeedbackCard() {
  const reasons = ["shallow", "missing evidence", "hallucinated evidence", "wrong SEV", "wrong root cause", "bad remediation", "ignored runbook", "repeated failed past action", "other"];
  const ratingIcon = (value) => /^(Useful|Correct)$/.test(value) ? "thumbs-up" : /^(Not Useful|Wrong)$/.test(value) ? "thumbs-down" : "minus";
  const ratingButtons = (field, values) => `<div class="rating-choice-row" role="group" aria-label="${field === "usefulness" ? "Analysis usefulness" : "Recommendation correctness"}">${values.map((value) => `<button class="rating-choice" type="button" aria-pressed="false" data-feedback-choice data-feedback-field="${field}" data-feedback-value="${escapeHtml(value)}"><span data-icon="${ratingIcon(value)}"></span>${escapeHtml(value)}</button>`).join("")}</div>`;
  return `<details class="analysis-card feedback-card" open><summary><span><span data-icon="check"></span><strong data-feedback-heading>Rate this analysis</strong></span><small>Help improve future incident guidance</small></summary><div class="feedback-body">
    <input type="hidden" data-feedback-usefulness><input type="hidden" data-feedback-correctness>
    <div class="feedback-rating"><span>Was the analysis useful?</span>${ratingButtons("usefulness", ["Useful", "Partially Useful", "Not Useful"])}</div>
    <div class="feedback-rating"><span>Were the recommendations correct?</span>${ratingButtons("correctness", ["Correct", "Partially Correct", "Wrong"])}</div>
    <fieldset><legend>What influenced your rating?</legend><div class="reason-tags">${reasons.map((reason) => `<label><input type="checkbox" value="${escapeHtml(reason)}" data-feedback-reason><span>${escapeHtml(reason)}</span></label>`).join("")}</div></fieldset>
    <label>Recommendation being rated <span class="optional-label">Optional</span><input data-feedback-recommendation placeholder="Paste or summarize the recommendation"></label>
    <label>Additional context <span class="optional-label">Optional</span><textarea data-feedback-comments placeholder="What helped, or what should change?"></textarea></label>
    <div class="feedback-actions"><button type="button" data-submit-feedback>Save feedback</button><p class="meta" data-feedback-status>Your rating is stored with this analysis.</p></div>
  </div></details>`;
}

async function submitAnalysisFeedback(card) {
  if (!currentIncidentId || !card) return;
  const payload = {
    analysisUsefulness: card.querySelector("[data-feedback-usefulness]")?.value || "",
    recommendationCorrectness: card.querySelector("[data-feedback-correctness]")?.value || "",
    reasonTags: [...card.querySelectorAll("[data-feedback-reason]:checked")].map((item) => item.value),
    recommendationDescription: emptyToNull(card.querySelector("[data-feedback-recommendation]")?.value),
    comments: emptyToNull(card.querySelector("[data-feedback-comments]")?.value)
  };
  if (!payload.analysisUsefulness || !payload.recommendationCorrectness) {
    showToast("Feedback incomplete", "Select both ratings before saving.", "warning");
    return;
  }
  try {
    await requestJson(`/api/incidents/${encodeURIComponent(currentIncidentId)}/feedback`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
    card.querySelector("[data-feedback-status]").textContent = "Feedback saved.";
    card.querySelector("[data-submit-feedback]").disabled = true;
    card.querySelector("[data-feedback-heading]").textContent = "Feedback saved";
    card.dataset.saved = "true";
    card.open = false;
    showToast("Feedback saved", "Your rating was attached to this analysis.", "success");
    void loadRecent();
  } catch (error) { showToast("Feedback failed", error.message || String(error), "error"); }
}

function renderTimeline(events = []) {
  if (!events?.length) return "";
  return `<section class="analysis-card timeline-card"><h3><span data-icon="history"></span>Timeline</h3>${events.map((item) => `<div class="timeline-event"><time>${escapeHtml(new Date(item.occurredAtUtc).toLocaleString())}</time><div><strong>${escapeHtml(item.type)}</strong><p>${escapeHtml(item.summary)}</p>${item.evidenceReference ? `<code>${escapeHtml(item.evidenceReference)}</code>` : ""}</div></div>`).join("")}</section>`;
}

function renderKnowledgeUpdate(incidentId, proposal) {
  if (!proposal) return "";
  const pending = proposal.status === "pending";
  const publishedFile = `approved-${String(proposal.id || "").replaceAll("-", "")}.md`;
  return `<section class="analysis-card knowledge-card"><div class="knowledge-heading"><div><p class="eyebrow">Resolution learning</p><h3><span data-icon="book"></span>Proposed runbook / postmortem update</h3></div><span class="badge knowledge-status knowledge-${escapeHtml(proposal.status)}">${escapeHtml(proposal.status)}</span></div><p>Review this draft before it can become reusable incident knowledge. Rejected drafts are excluded from RAG and similarity.</p><label>Edit proposed update<textarea data-knowledge-content ${pending ? "" : "readonly"}>${escapeHtml(proposal.content)}</textarea></label>${pending ? `<div class="knowledge-actions"><button type="button" data-knowledge-review="approved" data-incident-id="${escapeHtml(incidentId)}">Approve and publish</button><button class="secondary danger-outline" type="button" data-knowledge-review="rejected" data-incident-id="${escapeHtml(incidentId)}">Reject draft</button></div>` : `<p class="system-notice ${proposal.status === "approved" ? "notice-success" : "notice-neutral"}"><strong>${proposal.status === "approved" ? "Published to runbook knowledge" : "Not published"}</strong><span>${proposal.status === "approved" ? `${escapeHtml(publishedFile)} is stored in the configured runbook knowledge folder and indexed on the next RAG search.` : "This rejected update will not be used by RAG or similarity."}</span></p>${proposal.status === "approved" ? `<button class="secondary compact-button" type="button" data-view-published-knowledge="${escapeHtml(proposal.title)}"><span data-icon="search"></span>View published runbook in RAG</button>` : ""}`}</section>`;
}

async function reviewKnowledgeUpdate(incidentId, decision) {
  const content = elements.historyDetail.querySelector("[data-knowledge-content]")?.value || null;
  const approving = decision === "approved";
  const confirmed = await showConfirmation({
    title: approving ? "Approve and publish this update?" : "Reject this proposed update?",
    message: approving ? "The edited draft will become reusable knowledge for future RAG and similar-incident analysis." : "The draft will remain in the incident record but will not be reusable knowledge.",
    confirmLabel: approving ? "Approve and publish" : "Reject draft",
    destructive: !approving
  });
  if (!confirmed) return;
  try {
    await requestJson(`/api/incidents/${encodeURIComponent(incidentId)}/knowledge-review`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ decision, content }) });
    await loadRecent();
    const item = recentAnalyses.find((analysis) => analysis.incidentId === incidentId);
    if (item) renderHistoryDetail(item);
    showToast(approving ? "Knowledge published" : "Draft rejected", approving ? "The approved update is now available to future retrieval." : "The draft will not be used by future retrieval.", "success");
  } catch (error) { showToast("Review failed", error.message || String(error), "error"); }
}

function renderIncidentCompare(incidentId) {
  const panel = $("#comparePanel");
  if (!panel.hidden && panel.dataset.incidentId === incidentId) {
    panel.hidden = true;
    panel.dataset.incidentId = "";
    panel.innerHTML = "";
    return;
  }
  const currentTitle = elements.incidentForm.title.value || "Current incident";
  const match = (window.currentSimilarIncidents || []).find((item) => item.incidentId === incidentId);
  if (!match) { panel.hidden = false; panel.innerHTML = `<p class="error-box">Comparison data is unavailable.</p>`; return; }
  panel.hidden = false;
  panel.dataset.incidentId = incidentId;
  panel.innerHTML = `<h4>Compare ${escapeHtml(shortenId(incidentId))}</h4><div class="compare-grid"><div><span class="meta">Current</span><strong>${escapeHtml(currentTitle)}</strong><p>${escapeHtml(elements.incidentForm.description.value || "No description provided.")}</p></div><div><span class="meta">Previous</span><strong>${escapeHtml(match.incidentSummary)}</strong><p>${escapeHtml(match.serviceName)} / ${escapeHtml(match.environment)} · ${escapeHtml(new Date(match.createdAtUtc).toLocaleString())}</p></div></div><p class="compare-callout">Shared signals: ${escapeHtml((match.sharedSignals || []).join(", ") || "none reported")}. Similarity score: ${Math.round(Number(match.score) * 100)}%.</p>${match.successfulActions?.length ? `<p><strong>Successful:</strong> ${escapeHtml(match.successfulActions.join("; "))}</p>` : ""}${match.failedActions?.length ? `<p class="danger-text"><strong>Failed:</strong> ${escapeHtml(match.failedActions.join("; "))}</p>` : ""}`;
}

async function loadRecent() {
  const separator = projectQuery("&");
  recentAnalyses = normalizeArray(await requestJson(`/api/incidents/recent?maxResults=12${separator}`));
  historyRows = recentAnalyses.map(toHistoryRow);
  populateHistoryFilters(historyRows);
  renderHistory();
  updateCounts();
  renderDetected();
}

function populateHistoryFilters(rows) {
  setSelectOptions(elements.historyServiceFilter, "All services", uniqueValues(rows.map((row) => row.service)));
  setSelectOptions(elements.historyStatusFilter, "All statuses", incidentLifecycleStatuses, formatStatusLabel);
  setSessionFilterOptions(rows);
  setSelectOptions(elements.historySeverityFilter, "All severities", severityFilterValues, formatSeverityLabel);
}

function renderHistory() {
  const rows = filterHistoryRows(historyRows);
  const page = paginateRows(rows, historyPage);
  historyPage = page.page;
  elements.historyTotal.textContent = recentAnalyses.length;
  elements.historyResultCount.textContent = `${rows.length} result${rows.length === 1 ? "" : "s"}`;
  elements.recentOutput.innerHTML = renderPendingKnowledgeReviews() + renderHistoryTable(page.items, rows.length) + renderPagination("history", page, "data-history-page");
  hydrateIcons(elements.recentOutput);
}

function filterHistoryRows(rows) {
  const query = elements.historySearch.value.trim().toLowerCase();
  const service = elements.historyServiceFilter.value;
  const status = elements.historyStatusFilter.value;
  const session = elements.historySessionFilter.value;
  const severity = elements.historySeverityFilter.value;
  return rows.filter((row) =>
    (!query || [row.incidentId, row.summary, row.description, row.service, row.environment, row.severity, row.status, row.sessionId, ...(row.tags || [])]
      .filter(Boolean)
      .some((value) => String(value).toLowerCase().includes(query))) &&
    (service === "all" || row.service === service) &&
    (status === "all" || row.status === status) &&
    matchesSessionFilter(row, session) &&
    (severity === "all" || row.severity === severity));
}

function renderHistoryTable(rows, totalRows = rows.length) {
  if (!historyRows.length) return `<div class="empty-state">No saved incidents yet. Use Create Incident or analyze a backlog row to populate history.</div>`;
  if (!totalRows) return `<div class="empty-state">No incidents match the current filters.</div>`;
  return `<div class="history-table-wrap"><table class="history-table"><thead><tr><th>ID</th><th>Incident</th><th>Service</th><th>Severity</th><th>Status</th><th>Provider</th><th>Confidence</th></tr></thead><tbody>${rows.map(renderHistoryRow).join("")}</tbody></table></div>`;
}

function renderHistoryRow(row) {
  const thread = getSessionThread(row.sessionId);
  const connected = thread.count > 1;
  const threadStyle = connected ? ` style="--thread-hue:${sessionHue(row.sessionId)}"` : "";
  const relationship = row.sessionTurnNumber > 1 ? `Follow-up ${row.sessionTurnNumber - 1}` : "Original";
  return `<tr class="${connected ? "session-connected" : ""}"${threadStyle} data-history-id="${escapeHtml(row.incidentId)}" tabindex="0" aria-label="Open ${escapeHtml(row.summary)}"><td><span class="history-id">${escapeHtml(row.displayId)}</span>${connected ? `<span class="thread-marker"><span data-icon="link"></span>${thread.count} linked · ${relationship}</span>` : ""}</td><td><button class="link-button" data-history-id="${escapeHtml(row.incidentId)}">${escapeHtml(trimTitle(row.summary))}</button><p class="history-description">${escapeHtml(row.description || "No description provided.")}</p><small>${row.tags.map((tag) => `<span>#${escapeHtml(tag)}</span>`).join(" ")}</small></td><td>${escapeHtml(row.service)}</td><td><span class="severity severity-${escapeHtml(row.severity)}">${escapeHtml(formatSeverityLabel(row.severity))}</span></td><td>${renderStatusText(row.status)}</td><td><span class="badge provider-${escapeHtml(row.provider)}">${escapeHtml(row.provider)}</span></td><td><span class="confidence-badge confidence-${escapeHtml(row.confidence)}">${escapeHtml(row.confidence)}</span></td></tr>`;
}

function getSessionThread(sessionId) {
  const members = recentAnalyses.filter((analysis) => analysis.sessionId === sessionId);
  return { count: members.length };
}

function matchesSessionFilter(row, filter) {
  const linkedCount = getSessionThread(row.sessionId).count;
  if (filter === "linked") return linkedCount > 1;
  if (filter === "standalone") return linkedCount <= 1;
  if (filter.startsWith("session:")) return row.sessionId === filter.slice("session:".length);
  return true;
}

function setSessionFilterOptions(rows) {
  const previous = elements.historySessionFilter.value;
  const sessions = [...new Set(rows.map((row) => row.sessionId).filter(Boolean))]
    .map((sessionId) => ({ sessionId, count: getSessionThread(sessionId).count }))
    .filter((session) => session.count > 1)
    .sort((a, b) => b.count - a.count);
  const sessionValues = sessions.map((session) => `session:${session.sessionId}`);
  elements.historySessionFilter.innerHTML = `<option value="all">All sessions</option><option value="linked">Linked incidents</option><option value="standalone">Standalone incidents</option>${sessions.map((session) => `<option value="session:${escapeHtml(session.sessionId)}">${escapeHtml(shortenId(session.sessionId))} (${session.count} linked)</option>`).join("")}`;
  elements.historySessionFilter.value = ["all", "linked", "standalone", ...sessionValues].includes(previous) ? previous : "all";
}

function sessionHue(sessionId) {
  let hash = 0;
  for (const character of String(sessionId || "")) hash = ((hash << 5) - hash + character.charCodeAt(0)) | 0;
  return Math.abs(hash) % 360;
}

function renderStatusText(currentStatus) {
  const normalized = normalizeIncidentStatus(currentStatus);
  return `<span class="ticket-status-text status-text-${escapeHtml(normalized)}">${escapeHtml(formatStatusLabel(normalized))}</span>`;
}

function renderLoadingState(message) {
  return `<div class="loading-state"><span class="loading-spinner" aria-hidden="true"></span><span>${escapeHtml(message)}</span></div>`;
}

function renderPendingKnowledgeReviews() {
  const pending = recentAnalyses.filter(item => item.status === "resolved" && item.proposedKnowledgeUpdate?.status === "pending");
  if (!pending.length) return "";
  return `<section class="learning-review-banner"><div><strong><span data-icon="book"></span>${pending.length} runbook update${pending.length === 1 ? "" : "s"} awaiting review</strong><p>Open a resolved incident to edit, approve, or reject its proposed reusable knowledge.</p></div><div>${pending.slice(0, 3).map(item => `<button class="secondary compact-button" type="button" data-history-id="${escapeHtml(item.incidentId)}">Review ${escapeHtml(formatHistoryId(item.incidentId, 0))}</button>`).join("")}</div></section>`;
}

function startAnalysisWait(initialMessage) {
  stopAnalysisWait();
  const startedAt = Date.now();
  const update = () => {
    const elapsed = Math.floor((Date.now() - startedAt) / 1000);
    let message = initialMessage;
    if (elapsed >= 35) message = `OpenRouter is still responding (${elapsed}s). The result will identify any timeout or fallback.`;
    else if (elapsed >= 15) message = `OpenRouter model request in progress (${elapsed}s)...`;
    elements.analysisStatus.innerHTML = `<span class="loading-spinner" aria-hidden="true"></span><span>${escapeHtml(message)}</span>`;
  };
  update();
  analysisWaitTimer = window.setInterval(update, 1000);
}

function stopAnalysisWait() {
  if (analysisWaitTimer) window.clearInterval(analysisWaitTimer);
  analysisWaitTimer = null;
}

function renderTicketActions(incidentId, currentStatus) {
  const normalized = normalizeIncidentStatus(currentStatus);
  const actionsByStatus = {
    new: [["active", "Start work"], ["resolved", "Resolve"]],
    active: [["mitigated", "Mark mitigated"], ["resolved", "Resolve"]],
    mitigated: [["active", "Reopen"], ["resolved", "Resolve"]],
    resolved: [["active", "Reopen"]]
  };
  const actions = actionsByStatus[normalized] || actionsByStatus.active;
  return `<div class="ticket-actions">${actions.map(([status, label]) => `<button class="secondary compact-button ticket-action ticket-action-${escapeHtml(status)}" type="button" data-ticket-status="${status}" data-incident-id="${escapeHtml(incidentId)}">${escapeHtml(label)}</button>`).join("")}</div>`;
}

async function deleteIncident(incidentId) {
  const incident = recentAnalyses.find((analysis) => analysis.incidentId === incidentId);
  const label = incident?.incidentSummary || "this incident";
  const confirmed = await showConfirmation({
    title: "Permanently delete this incident?",
    message: `“${label}” will be removed from history and future similarity retrieval. Other incidents in the linked session remain available.`,
    confirmLabel: "Delete incident",
    destructive: true
  });
  if (!confirmed) return;

  try {
    await requestJson(`/api/incidents/${encodeURIComponent(incidentId)}`, { method: "DELETE" });
    recentAnalyses = recentAnalyses.filter((analysis) => analysis.incidentId !== incidentId);
    historyRows = historyRows.filter((row) => row.incidentId !== incidentId);
    populateHistoryFilters(historyRows);
    closeHistoryModal();
    renderHistory();
    updateCounts();
    renderDetected();
    showToast("Incident deleted", "The saved incident was removed from history.", "success");
  } catch (error) {
    showToast("Delete failed", error.message || String(error), "error");
  }
}

async function updateIncidentStatus(incidentId, status) {
  if (!incidentId) return;
  try {
    const result = await requestJson(`/api/incidents/${encodeURIComponent(incidentId)}/status`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ status })
    });
    const normalized = normalizeIncidentStatus(result?.status || status);
    await loadRecent();
    dashboardPage = 1;
    historyPage = 1;
    const activeItem = recentAnalyses.find((analysis) => analysis.incidentId === incidentId);
    if (activeItem && !elements.historyModal.hidden) renderHistoryDetail(activeItem);
    showToast("Ticket updated", `Status set to ${formatStatusLabel(normalized)}.`, "success");
  } catch (error) {
    showToast("Status not saved", error.message || String(error), "error");
  }
}

function renderHistoryDetail(item) {
  if (elements.historyModal.hidden) historyModalReturnFocus = document.activeElement;
  const actions = item.recommendedActions || [];
  const hypotheses = item.hypotheses || [];
  const evidence = item.evidence || [];
  const linkedAnalyses = recentAnalyses.filter((analysis) => analysis.sessionId === item.sessionId).length;
  elements.historyDetail.innerHTML = `<p class="eyebrow">${item.sessionTurnNumber > 1 ? `Follow-up analysis ${item.sessionTurnNumber - 1}` : "Original analysis"}</p><div class="modal-title-row"><div><h3 id="historyModalTitle">${escapeHtml(item.incidentTitle || item.incidentSummary)}</h3>${renderStatusText(item.status || "active")}</div>${renderTicketActions(item.incidentId, item.status || "active")}</div><p class="incident-detail-description">${escapeHtml(item.incidentDescription || "No description provided.")}</p>${item.incidentSummary && item.incidentSummary !== item.incidentTitle ? `<p class="analysis-summary"><strong>Analysis summary:</strong> ${escapeHtml(item.incidentSummary)}</p>` : ""}<div class="session-link-row"><div class="session-identity"><span><span class="live-dot"></span>Linked session</span><code title="${escapeHtml(item.sessionId)}">${escapeHtml(item.sessionId)}</code></div><div class="session-count"><strong>Turn ${item.sessionTurnNumber}</strong><span>${linkedAnalyses} linked ${linkedAnalyses === 1 ? "analysis" : "analyses"}</span></div><button class="icon-refresh-button" type="button" data-copy-session="${escapeHtml(item.sessionId)}" aria-label="Copy session ID"><span data-icon="copy"></span></button><button class="secondary compact-button" type="button" data-follow-up-session="${escapeHtml(item.sessionId)}">Continue session</button></div>${renderKnowledgeUpdate(item.incidentId, item.proposedKnowledgeUpdate)}${renderProviderTransparency(item.providerTransparency)}${renderRecommendedActions(actions, false)}${renderGroundedFacts(item.knownFacts)}${renderHypotheses(hypotheses)}${renderAnalysisBlock("Unknowns & validation gaps", item.unknowns, "info")}${renderStoredEvidenceBlock(evidence)}${renderRunbookMatches(item.runbookMatches)}${renderAnalysisQuality(item.quality)}${renderPriorActions(item.similarIncidents)}${renderOutcomeHistory(item.actionOutcomes)}${renderFeedbackHistory(item.feedback)}${renderTimeline(item.timeline)}<section class="danger-zone"><div><strong>Delete incident</strong><p>Remove this incident from history and future similarity matches.</p></div><button class="compact-button delete-incident-button" type="button" data-delete-incident="${escapeHtml(item.incidentId)}"><span data-icon="trash"></span>Delete incident</button></section>`;
  elements.historyModal.hidden = false;
  hydrateIcons(elements.historyDetail);
  requestAnimationFrame(() => elements.historyModalClose.focus());
}

function renderFeedbackHistory(feedback = []) {
  if (!feedback.length) return "";
  return `<section class="analysis-card"><h3><span data-icon="check"></span>Saved feedback (${feedback.length})</h3>${feedback.map((item) => `<div class="grounded-item"><strong>${escapeHtml(item.analysisUsefulness)} / ${escapeHtml(item.recommendationCorrectness)}</strong><p>${escapeHtml((item.reasonTags || []).join(", ") || "No reason tags")}</p><small>${escapeHtml(item.comments || "No comment")} · ${escapeHtml(new Date(item.submittedAtUtc).toLocaleString())}</small></div>`).join("")}</section>`;
}

function renderStoredEvidenceBlock(evidence) {
  const rows = normalizeArray(evidence).slice(0, 8);
  if (!rows.length) return "";
  return `<section class="analysis-card evidence-card"><h3><span data-icon="activity"></span>Saved evidence (${rows.length})</h3>${rows.map((item) => `<div class="stored-evidence-line"><strong>${escapeHtml(formatEvidenceSource(item.source))}</strong><p>${escapeHtml(item.summary || item.description || item.details || "Evidence item")}</p>${item.details ? `<small>${escapeHtml(item.details)}</small>` : ""}</div>`).join("")}</section>`;
}

function closeHistoryModal() {
  elements.historyModal.hidden = true;
  const returnFocus = historyModalReturnFocus;
  historyModalReturnFocus = null;
  if (returnFocus?.isConnected) returnFocus.focus();
}

function showConfirmation({ title, message, confirmLabel = "Confirm", destructive = false }) {
  if (confirmationResolver) closeConfirmation(false);
  confirmationReturnFocus = document.activeElement;
  elements.confirmModalTitle.textContent = title;
  elements.confirmModalMessage.textContent = message;
  elements.confirmModalAccept.textContent = confirmLabel;
  elements.confirmModalAccept.classList.toggle("danger-button", destructive);
  elements.confirmModalAccept.classList.toggle("primary-confirm-button", !destructive);
  elements.confirmModal.classList.toggle("confirm-destructive", destructive);
  elements.confirmModal.hidden = false;
  requestAnimationFrame(() => (destructive ? elements.confirmModalCancel : elements.confirmModalAccept).focus());
  return new Promise((resolve) => { confirmationResolver = resolve; });
}

function closeConfirmation(confirmed) {
  if (elements.confirmModal.hidden && !confirmationResolver) return;
  elements.confirmModal.hidden = true;
  const resolve = confirmationResolver;
  const returnFocus = confirmationReturnFocus;
  confirmationResolver = null;
  confirmationReturnFocus = null;
  resolve?.(confirmed);
  if (returnFocus?.isConnected) returnFocus.focus();
}

function getModalFocusableElements(modal = elements.historyModal) {
  return [...modal.querySelectorAll('button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])')]
    .filter((element) => !element.hidden && element.getClientRects().length > 0);
}

function renderSourcesPage(items) {
  const warning = items.some((item) => item.isDemoMode) ? `<div class="warning-banner"><span data-icon="alert"></span>Sample data active - logs and metrics are bundled sample files. Connect real sources for production use.</div>` : "";
  const projectCards = projects.map((project) => `<article class="project-source-card"><div><h3>${escapeHtml(project.name || project.id)} <span class="badge project-badge">${escapeHtml(project.id)}</span></h3><p>${escapeHtml(project.sourceHealthEndpoint || "No health endpoint configured")}</p><div class="badge-row"><span class="badge">logs: ${escapeHtml(project.logEntriesPath ? "configured" : "default")}</span><span class="badge">metrics: ${escapeHtml(project.metricSamplesPath ? "configured" : "default")}</span><span class="badge">threshold: errors ${escapeHtml(project.thresholds?.highErrorRateThreshold ?? "?")}</span></div></div>${project.removable ? `<button class="secondary compact-button danger-outline" type="button" data-remove-project="${escapeHtml(project.id)}"><span data-icon="trash"></span>Remove</button>` : `<span class="badge muted">Configured</span>`}</article>`).join("");
  return `${warning}<section class="project-source-grid">${projectCards}</section>${items.map(renderSourceCard).join("")}<section class="setup-section"><div class="setup-heading"><div><h3><span data-icon="plug"></span>Source configuration</h3><p>Source locations are read-only here because configuration is managed in appsettings or environment variables. Configured means a path is set; connected means the API verified it.</p></div></div><div class="setup-steps"><span><strong>1</strong> Configure paths</span><span><strong>2</strong> Refresh monitor</span><span><strong>3</strong> Verify RAG diagnostics</span></div></section>`;
}

function renderSourceCard(item) {
  const modeClass = item.isDemoMode ? "badge-warning" : "status-connected";
  const statusClass = item.status === "missing" || item.status === "unavailable" ? "status-missing" : item.status === "pending" ? "status-warning" : "status-connected";
  return `<article class="source-card figma-source"><div><h3>${escapeHtml(item.name)} <span class="badge ${modeClass}">${escapeHtml(item.mode)}</span> <span class="badge ${statusClass}">${escapeHtml(item.status)}</span></h3><p>${escapeHtml(item.location)}</p><div class="badge-row">${(item.capabilities || []).map((cap) => `<span class="badge">${escapeHtml(cap)}</span>`).join("")}</div></div><span class="badge">${escapeHtml(item.type)}</span></article>`;
}

async function sendLogSignal() {
  const form = new FormData(elements.logSignalForm);
  const payload = Object.fromEntries(form.entries());
  payload.projectId = activeProjectId === "all" ? "default" : activeProjectId;
  await requestJson("/api/signals/logs", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
  setFeedback(elements.ingestionFeedback, "Log accepted", "Signal written. Rescanning backlog.", "connected");
  await loadDetected(false, true);
}

async function sendMetricSignal() {
  const form = new FormData(elements.metricSignalForm);
  const payload = Object.fromEntries(form.entries());
  delete payload.unit;
  payload.value = Number(payload.value);
  payload.projectId = activeProjectId === "all" ? "default" : activeProjectId;
  await requestJson("/api/signals/metrics", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
  setFeedback(elements.ingestionFeedback, "Metric accepted", "Sample written. Rescanning backlog.", "connected");
  await loadDetected(false, true);
}

async function searchRag() {
  const query = new FormData(elements.ragForm).get("query");
  if (!String(query || "").trim()) return;
  const result = await requestJson(`/api/runbooks/search?query=${encodeURIComponent(query)}&maxResults=8`);
  const provider = String(result.vectorStoreProvider || "sqlite");
  const isQdrant = provider.toLowerCase().includes("qdrant");
  elements.ragSummary.innerHTML = `<dl class="metric-strip diag-strip"><div><dt>Embedding Provider</dt><dd>${escapeHtml(result.embeddingProvider || "unknown")}</dd><p>${escapeHtml(result.embeddingModel || "model not reported")}</p></div><div><dt>Vector Store</dt><dd>${escapeHtml(provider)}</dd><p>${escapeHtml(result.databasePath || "path not reported")}</p></div><div><dt>RAG Status</dt><dd><span class="${result.isDegraded ? "idle-dot" : "live-dot"}"></span> ${escapeHtml(result.ragStatus || "unknown")}</dd><p>${escapeHtml(result.knowledgeBasePath || "knowledge base not reported")}</p></div></dl>${result.isDegraded ? `<div class="warning-banner"><span data-icon="alert"></span>RAG degraded: ${escapeHtml(result.degradedReason || "retrieval fallback active")}</div>` : ""}`;
  const rows = normalizeArray(result.matches);
  elements.ragResults.innerHTML = `<div class="section-title stacked rag-title"><h3><span data-icon="search"></span>Match Scores</h3><span class="meta">Actual runbook chunks returned by the retrieval API for this query</span></div>${rows.map((item) => renderRagMatch(item)).join("") || `<div class="empty-state">No runbook chunks matched the current query.</div>`}`;
  hydrateIcons(elements.ragSummary);
  hydrateIcons(elements.ragResults);
}

function renderRagMatch(item) {
  const score = Number(item.score) || 0;
  const color = score >= 0.75 ? "green" : score >= 0.5 ? "yellow" : "red";
  const label = item.sectionPath || (item.tags || [])[0] || "match";
  return `<article class="result-item rag-match"><div><h3 title="${escapeHtml(item.source || item.runbookId)}"><span class="runbook-name">${escapeHtml(shortRunbookName(item.source || item.runbookId))}</span><span class="badge">${escapeHtml(label)}</span></h3><p>${escapeHtml(item.summary || item.title || "Runbook chunk")}</p></div><div class="match-score score-${color}"><strong>${score.toFixed(2)}</strong><span class="mini-bar score-${color}"><i style="width:${Math.max(2, Math.min(100, score * 100))}%"></i></span></div></article>`;
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
  elements.incidentForm.severity.value = item.title.includes("DB") ? "sev1" : "sev2";
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
  const lastScanTime = state.scannedAt ? state.scannedAt.toLocaleString([], { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit", second: "2-digit" }) : "Not run";
  return `<dl class="metric-strip monitor-strip"><div><dt>Monitor Status</dt><dd><span class="${active ? "live-dot" : "idle-dot"}"></span>${active ? "Active" : "Paused"}</dd></div><div><dt>Polling Interval</dt><dd class="interval-value">${escapeHtml(elements.pollingSlider.value)}s</dd></div><div><dt>Last Scan</dt><dd>${escapeHtml(lastScanTime)}</dd></div></dl><article class="scan-result"><h3><span data-icon="activity"></span>Last Scan Result</h3><p>Scanned ${state.scannedSources} sources - found ${state.signalsFound} signals, ${state.errors} errors.</p><p class="scan-stat-line"><span>Signals found: <strong>${state.signalsFound}</strong></span><span>Errors: <strong class="${state.errors ? "danger-text" : "success-text"}">${state.errors}</strong></span><span>Duration: <strong>${state.durationSeconds.toFixed(1)}s</strong></span></p></article>`;
}

function inferProviderMode(result) {
  const provider = String(result.analysisProvider || "");
  const reason = String(result.fallbackReason || "");
  if (provider.includes("deterministic-structured-fallback") || reason.includes("deterministic structured")) return { label: "Structured fallback", className: "status-warning", badgeClass: "status-local", description: "Model JSON was invalid; deterministic fields are displayed." };
  if (result.usedFallbackAnalysis) return { label: "Local fallback", className: "status-local", badgeClass: "status-local", description: "The local evidence analyzer produced this result." };
  return { label: "Model-backed", className: "status-model", badgeClass: "status-model", description: "Structured output came from the configured model." };
}

function formatProviderMessage(result, mode) {
  const reason = String(result.fallbackReason || "");
  const ragDegraded = Boolean(result.providerTransparency?.isDegraded);
  const ragNote = ragDegraded ? " RAG continued with degraded embeddings." : "";
  if (reason.includes("API key is not configured")) return `No model key was available; local evidence analysis was used.${ragNote}`;
  if (/timed out|timeout/i.test(reason)) return `OpenRouter timed out; local evidence analysis was used.${ragNote}`;
  if (reason.includes("empty analysis response") || reason.includes("empty output") || reason.includes("empty message")) return "The model returned an empty response, so local analysis used the gathered evidence instead.";
  if (reason.includes("429") || reason.includes("Too Many Requests") || reason.includes("rate-limited")) return "The model provider is rate-limited, so local analysis used the gathered evidence instead.";
  return result.usedFallbackAnalysis ? `${mode.description}${ragNote}` : `${reason || mode.description}${ragNote}`;
}

async function loadRunbookSources() {
  try {
    const sources = normalizeArray(await requestJson("/api/runbooks/sources"));
    elements.runbookSources.innerHTML = sources.map(renderRunbookSource).join("") || `<div class="empty-state">No runbook sources are connected.</div>`;
    hydrateIcons(elements.runbookSources);
  } catch (error) {
    elements.runbookSources.innerHTML = `<div class="error-box">${escapeHtml(error.message || String(error))}</div>`;
  }
}

function renderRunbookSource(source) {
  const statusClass = source.reachable ? "status-connected" : "status-missing";
  const enabledLabel = source.enabled ? "Enabled" : "Disabled";
  return `<article class="runbook-source-card"><div><div class="badge-row"><strong>${escapeHtml(source.name)}</strong><span class="badge">${escapeHtml(source.type)}</span><span class="badge ${statusClass}">${source.reachable ? "Reachable" : "Unavailable"}</span><span class="badge">${enabledLabel}</span></div><p>${escapeHtml(source.path)}</p><small>${Number(source.documentCount) || 0} documents · ${Number(source.sectionCount) || 0} indexed sections · Last synchronized ${source.lastSynchronizedAtUtc ? escapeHtml(formatTimestamp(source.lastSynchronizedAtUtc)) : "not yet"}</small>${source.lastError ? `<div class="error-box compact-error">${escapeHtml(source.lastError)}</div>` : ""}</div><div class="runbook-source-actions"><button class="secondary compact-button" type="button" data-runbook-source-action="sync" data-source-id="${escapeHtml(source.id)}"><span data-icon="refresh"></span>Sync</button>${source.removable ? `<button class="secondary compact-button" type="button" data-runbook-source-action="toggle" data-enabled="${source.enabled}" data-source-id="${escapeHtml(source.id)}">${source.enabled ? "Disable" : "Enable"}</button><button class="secondary compact-button danger-outline" type="button" data-runbook-source-action="remove" data-source-id="${escapeHtml(source.id)}"><span data-icon="trash"></span>Remove</button>` : ""}</div></article>`;
}

async function connectRunbookSource() {
  const payload = Object.fromEntries(new FormData(elements.runbookSourceForm).entries());
  try {
    await requestJson("/api/runbooks/sources", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
    elements.runbookSourceForm.reset();
    await loadRunbookSources();
    showToast("Runbook source connected", "Verify and synchronize it before relying on its knowledge.", "success");
  } catch (error) { showToast("Source connection failed", error.message || String(error), "error"); }
}

async function handleRunbookSourceAction(button) {
  const id = button.dataset.sourceId;
  const action = button.dataset.runbookSourceAction;
  if (action === "remove") {
    const confirmed = await showConfirmation({ title: "Remove this runbook source?", message: "Its indexed sections will be removed during the next synchronization. Source files will not be deleted.", confirmLabel: "Remove source", destructive: true });
    if (!confirmed) return;
  }
  button.disabled = true;
  try {
    if (action === "sync") await requestJson(`/api/runbooks/sources/${encodeURIComponent(id)}/synchronize`, { method: "POST" });
    if (action === "toggle") await requestJson(`/api/runbooks/sources/${encodeURIComponent(id)}/enabled`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ enabled: button.dataset.enabled !== "true" }) });
    if (action === "remove") await requestJson(`/api/runbooks/sources/${encodeURIComponent(id)}`, { method: "DELETE" });
    await loadRunbookSources();
    if (action === "sync") await searchRag();
    showToast("Runbook sources updated", action === "sync" ? "Source content was re-indexed." : "Source configuration was saved.", "success");
  } catch (error) { button.disabled = false; showToast("Runbook source update failed", error.message || String(error), "error"); }
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
function cleanStatusMessage(value) {
  const sentences = String(value || "").split(/(?<=[.!?])\s+/).map((item) => item.trim()).filter(Boolean);
  return [...new Map(sentences.map((item) => [item.toLowerCase(), item])).values()].join(" ");
}
function splitTags(value) { return String(value || "").split(",").map((tag) => tag.trim()).filter(Boolean); }
function emptyToNull(value) { const text = String(value || "").trim(); return text ? text : null; }
function escapeHtml(value) { return String(value ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#039;"); }
function formatTime(value) { return value ? new Date(value).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }) : ""; }
function formatTimestamp(value) { return value ? new Date(value).toLocaleString() : ""; }
function formatReadableTime(value) {
  if (!value) return "";
  return new Date(value).toLocaleTimeString([], { hour: "numeric", minute: "2-digit", second: "2-digit" });
}
function formatEvidenceTime(details) {
  const match = String(details || "").match(/\d{4}-\d{2}-\d{2}T[^\s]+/);
  return match ? formatReadableTime(match[0]) : "";
}
function formatAgo(value) {
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
function shortenId(value) { const text = String(value || ""); return text.length > 12 ? `${text.slice(0, 8)}...` : text || "none"; }
function formatIncidentTitle(title) { return String(title || "").replace("request error rate threshold breached", "error rate threshold").replace("queue depth threshold breached", "queue depth threshold").replace("suspicious log pattern", "log signal"); }
function formatEvidenceSource(source) { return String(source || "evidence").replace("tool.logs", "LOG").replace("tool.metrics", "METRIC").replace("incident.description", "INCIDENT").replace(/^rag\.runbook\./, "RUNBOOK ").replace(/^history\.incident\./, "HISTORY "); }
function evidenceKind(source) { return String(source || "").includes("metrics") ? "metric" : String(source || "").includes("logs") ? "log" : "generic"; }
function formatNotes(value) { return String(value || "No notes captured.").trim(); }
function trimTitle(value) { const text = String(value || "Untitled incident"); return text.length > 31 ? `${text.slice(0, 31)}...` : text; }
function normalizeAction(value) { return String(value || "").toLowerCase().replace(/[^a-z0-9]+/g, " ").trim(); }
function inferService(value) { const text = String(value || "").toLowerCase(); if (text.includes("auth")) return "auth-service"; if (text.includes("notification")) return "notification-worker"; if (text.includes("cdn")) return "cdn-edge"; if (text.includes("user")) return "user-service"; return "checkout-service"; }
function inferEnvironment(value) { const text = String(value || "").toLowerCase(); if (/\bstaging\b|\bstage\b/.test(text)) return "staging"; if (/\bdev\b|\bdevelopment\b/.test(text)) return "dev"; if (/\btest\b|\bqa\b/.test(text)) return "test"; return "prod"; }
function shortRunbookName(value) { const text = String(value || "runbook/checkout-db.md").replaceAll("\\", "/"); const match = text.match(/KnowledgeBase\/(.+)$/i); return match ? `runbook/${match[1]}` : text.split("/").slice(-2).join("/"); }
function parseJson(value) { try { return JSON.parse(value); } catch { return null; } }
function similarPercent(score, index) { return Math.round((Number(score) || [0.94, 0.81, 0.71][index] || 0.64) * 100); }
function scoreColor(percent) { return percent >= 90 ? "green" : percent >= 75 ? "yellow" : "red"; }
function formatHistoryId(value, index) { const text = String(value || ""); return text ? `INC-${text.replace(/-/g, "").slice(0, 4).toUpperCase()}` : `INC-${2847 - index}`; }
function inferSeverity(summary, parsed) { const text = `${summary || ""} ${JSON.stringify(parsed || {})}`.toLowerCase(); if (text.includes("critical")) return "sev1"; if (text.includes("5xx") || text.includes("latency")) return "sev2"; return "sev3"; }
function formatSeverityLabel(value) { const key = String(value || "").toLowerCase().replace("-", ""); return ({ sev1: "SEV-1", sev2: "SEV-2", sev3: "SEV-3", sev4: "SEV-4", sev5: "SEV-5" })[key] || "SEV-3"; }
function formatStatusLabel(value) { return ({ candidate: "Candidate", false_positive: "False positive", ignored: "Ignored", merged: "Merged", recovered: "Recovered", new: "Confirmed", active: "Active", mitigated: "Mitigated", resolved: "Resolved" })[String(value || "").toLowerCase()] || "Active"; }
function normalizeIncidentStatus(value) {
  const status = String(value || "active").toLowerCase();
  if (status === "ack") return "active";
  return ["new", "active", "mitigated", "resolved"].includes(status) ? status : "active";
}
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
  const sourceText = `${item.incidentTitle || ""} ${item.incidentSummary} ${item.notes} ${item.analysisText} ${(item.actionOutcomes || []).map((outcome) => `${outcome.status} ${outcome.description}`).join(" ")}`;
  return {
    item,
    incidentId: item.incidentId,
    displayId: formatHistoryId(item.incidentId, index),
    summary: item.incidentTitle || item.incidentSummary,
    description: item.incidentDescription || "",
    notes: formatNotes(item.notes),
    tags: item.tags?.length ? item.tags : inferHistoryTags(sourceText),
    service: item.serviceName || inferService(sourceText),
    environment: item.environment || inferEnvironment(sourceText),
    severity: String(item.severity || inferSeverity(item.incidentSummary, parsed)).toLowerCase(),
    status: normalizeIncidentStatus(item.status),
    provider: item.usedFallbackAnalysis ? "local" : "model",
    confidence: String(item.confidence || parsed.confidence || "medium").toLowerCase(),
    actionOutcomes: item.actionOutcomes || [],
    sessionId: item.sessionId,
    sessionTurnNumber: item.sessionTurnNumber,
    createdAtUtc: item.createdAtUtc,
    projectId: item.projectId || "default"
  };
}
function uniqueValues(values) { return [...new Set(values.filter(Boolean))].sort((a, b) => a.localeCompare(b)); }
function paginateRows(rows, requestedPage) {
  const pageCount = Math.max(1, Math.ceil(rows.length / pageSize));
  const page = Math.min(Math.max(1, Number(requestedPage) || 1), pageCount);
  const start = (page - 1) * pageSize;
  return { items: rows.slice(start, start + pageSize), page, pageCount, total: rows.length, start };
}
function renderPagination(name, page, attr) {
  if (page.total <= pageSize) return "";
  const previous = Math.max(1, page.page - 1);
  const next = Math.min(page.pageCount, page.page + 1);
  const start = page.start + 1;
  const end = Math.min(page.start + page.items.length, page.total);
  return `<div class="pagination-bar"><span>${start}-${end} of ${page.total}</span><div><button class="secondary compact-button" type="button" ${attr}="${previous}"${page.page === 1 ? " disabled" : ""}>Previous</button><button class="secondary compact-button" type="button" ${attr}="${next}"${page.page === page.pageCount ? " disabled" : ""}>Next</button></div></div>`;
}
function setSelectOptions(select, label, values, formatLabel = (value) => value) {
  const previous = select.value;
  select.innerHTML = `<option value="all">${escapeHtml(label)}</option>${values.map((value) => `<option value="${escapeHtml(value)}">${escapeHtml(formatLabel(value))}</option>`).join("")}`;
  select.value = values.includes(previous) ? previous : "all";
}
function renderError(target, error) { target.innerHTML = `<div class="error-box">${escapeHtml(error.message || String(error))}</div>`; }
function setFeedback(target, title, message, status) { target.innerHTML = `<span class="status-pill status-${status}">${escapeHtml(title)}</span><span>${escapeHtml(message)}</span>`; }
function showToast(title, message, tone = "info") { const toast = document.createElement("div"); toast.className = `toast toast-${tone}`; toast.setAttribute("role", tone === "error" ? "alert" : "status"); toast.innerHTML = `<strong>${escapeHtml(title)}</strong><span>${escapeHtml(message)}</span>`; elements.toast.appendChild(toast); setTimeout(() => toast.remove(), 4200); }
async function copyCodeText(button) {
  const text = button.closest(".code-row")?.querySelector("code")?.textContent || "";
  if (!text) return;
  await copyText(text, "Evidence copied");
}
async function copyText(text, successTitle = "Copied") {
  try {
    await navigator.clipboard.writeText(text);
    showToast(successTitle, "Copied to clipboard.", "success");
  } catch {
    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.style.position = "fixed";
    textarea.style.opacity = "0";
    document.body.appendChild(textarea);
    textarea.select();
    document.execCommand("copy");
    textarea.remove();
    showToast(successTitle, "Copied to clipboard.", "success");
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

function renderSidebarLastScan() {
  elements.sidebarLastScan.textContent = `Last scan: ${lastScanState?.scannedAt ? lastScanState.scannedAt.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" }) : "not run"}`;
}

async function initialize() {
  document.documentElement.dataset.theme = localStorage.getItem("incidentops.theme") === "dark" ? "dark" : "light";
  syncPollingControl();
  syncPollingButton();
  restoreLastScanState();
  hydrateIcons();
  await checkHealth();
  await loadProjects();
  await loadSources();
  await loadPersistedMonitoringState();
  elements.lastScan.innerHTML = renderMonitorSummary(detectedCandidates);
  renderSidebarLastScan();
  hydrateIcons(elements.lastScan);
  void loadRecent();
  void loadDetected(false, false);
  void searchRag();
  void loadEvaluation();
  const initialTab = location.hash.replace("#", "");
  if (initialTab && document.querySelector(`#${CSS.escape(initialTab)}View`)) {
    activateTab(initialTab);
  }
  startPolling();
}

async function loadPersistedMonitoringState() {
  try {
    const result = await requestJson(`/api/monitoring/state${projectQuery()}`);
    applyServerMonitoringState(result);
    const scan = result?.lastScan;
    if (!scan?.completedAtUtc) return;
    const persistedAt = new Date(scan.completedAtUtc);
    if (!Number.isFinite(persistedAt.getTime()) || (lastScanState?.scannedAt && lastScanState.scannedAt >= persistedAt)) return;
    lastScanState = {
      scannedSources: Number(scan.scannedSourceCount) || 0,
      connectedSources: Math.max(0, (Number(scan.scannedSourceCount) || 0) - (Number(scan.errorCount) || 0)),
      errors: Number(scan.errorCount) || 0,
      signalsFound: Number(scan.candidateCount) || 0,
      durationSeconds: Math.max(0, (Number(scan.durationMilliseconds) || 0) / 1000),
      scannedAt: persistedAt
    };
    saveLastScanState();
  } catch {
    // Keep the last verified local state when the persistence endpoint is unavailable.
  }
}

function syncPollingButton() {
  const button = $("#pauseScanButton");
  button.innerHTML = `<span data-icon="${polling ? "pause" : "play"}"></span>${polling ? "Pause Scanning" : "Resume Scanning"}`;
  button.dataset.hydrated = "";
  hydrateIcons(button);
}

function syncPollingControl() {
  const value = Number(elements.pollingSlider.value);
  const min = Number(elements.pollingSlider.min);
  const max = Number(elements.pollingSlider.max);
  const percent = ((value - min) / (max - min)) * 100;
  elements.pollingValue.textContent = `${value}s`;
  elements.pollingSlider.style.setProperty("--slider-progress", `${percent}%`);
}

function startPolling() {
  stopPolling();
  const intervalMs = 5000;
  pollingTimer = window.setInterval(() => {
    void refreshServerMonitoringView();
  }, intervalMs);
}

async function refreshServerMonitoringView() {
  try { await loadPersistedMonitoringState(); await loadDetected(false, false); }
  catch { /* keep the last verified server state */ }
}

function applyServerMonitoringState(state = {}) {
  polling = Boolean(state.enabled);
  if (Number(state.pollingIntervalSeconds) >= Number(elements.pollingSlider.min) && Number(state.pollingIntervalSeconds) <= Number(elements.pollingSlider.max)) elements.pollingSlider.value = String(state.pollingIntervalSeconds);
  syncPollingControl();
  syncPollingButton();
  if (state.lastScan?.completedAtUtc) {
    const scan = state.lastScan;
    lastScanState = { scannedSources: Number(scan.scannedSourceCount) || 0, connectedSources: Math.max(0, Number(scan.scannedSourceCount || 0) - Number(scan.errorCount || 0)), errors: Number(scan.errorCount) || 0, signalsFound: Number(scan.candidateCount) || 0, durationSeconds: Math.max(0, Number(scan.durationMilliseconds || 0) / 1000), scannedAt: new Date(scan.completedAtUtc) };
  }
  elements.lastScan.innerHTML = renderMonitorSummary(detectedCandidates);
  renderSidebarLastScan();
  hydrateIcons(elements.lastScan);
}

async function updateServerPollingInterval() {
  try {
    const state = await requestJson("/api/monitoring/interval", { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ seconds: Number(elements.pollingSlider.value) }) });
    applyServerMonitoringState(state);
    showToast("Polling interval updated", `The server will scan every ${state.pollingIntervalSeconds} seconds.`, "success");
  } catch (error) { showToast("Polling update failed", error.message || String(error), "error"); }
}

function stopPolling() {
  if (pollingTimer) {
    window.clearInterval(pollingTimer);
    pollingTimer = null;
  }
}

initialize();
