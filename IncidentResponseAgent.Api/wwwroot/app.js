const healthStatus = document.querySelector("#healthStatus");
const incidentForm = document.querySelector("#incidentForm");
const ragForm = document.querySelector("#ragForm");
const analysisOutput = document.querySelector("#analysisOutput");
const ragSummary = document.querySelector("#ragSummary");
const ragResults = document.querySelector("#ragResults");
const recentOutput = document.querySelector("#recentOutput");

document.querySelector("#sampleButton").addEventListener("click", () => {
  incidentForm.title.value = "Checkout 5xx spike";
  incidentForm.description.value = "Customers are seeing intermittent 500 responses during checkout. Error rate increased after the latest deployment.";
  incidentForm.severity.value = "High";
  incidentForm.serviceName.value = "checkout-api";
  incidentForm.environment.value = "production";
  incidentForm.tags.value = "checkout, 5xx, latency";
});

document.querySelector("#recentButton").addEventListener("click", loadRecent);

incidentForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  analysisOutput.innerHTML = `<div class="empty-state">Analyzing incident...</div>`;

  const form = new FormData(incidentForm);
  const tags = splitTags(form.get("tags"));
  const payload = {
    title: form.get("title"),
    description: form.get("description"),
    severity: form.get("severity"),
    serviceName: emptyToNull(form.get("serviceName")),
    environment: emptyToNull(form.get("environment")),
    sessionId: emptyToNull(form.get("sessionId")),
    tags
  };

  try {
    const result = await requestJson("/api/incidents/analyze", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    incidentForm.sessionId.value = result.sessionId;
    renderAnalysis(result);
    await loadRecent();
  } catch (error) {
    renderError(analysisOutput, error);
  }
});

ragForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  await searchRag();
});

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
    const results = await requestJson("/api/incidents/recent?maxResults=5");
    recentOutput.innerHTML = results.map((item) => `
      <article class="result-item">
        <h3>${escapeHtml(item.incidentSummary)}</h3>
        <p class="meta">Session ${escapeHtml(item.sessionId)} · Turn ${item.sessionTurnNumber} · ${escapeHtml(item.confidence || "unknown")} confidence</p>
        <p>${escapeHtml(item.notes || "")}</p>
      </article>
    `).join("") || `<div class="empty-state">No saved analyses yet.</div>`;
  } catch (error) {
    renderError(recentOutput, error);
  }
}

function renderAnalysis(result) {
  analysisOutput.className = "";
  analysisOutput.innerHTML = `
    <div class="section-block">
      <h3>Summary</h3>
      <p>${escapeHtml(result.incidentSummary)}</p>
      <p class="meta">Session ${escapeHtml(result.sessionId)} · Turn ${result.sessionTurnNumber} · ${escapeHtml(result.confidence || "unknown")} confidence</p>
    </div>
    <div class="section-block">
      <h3>Evidence</h3>
      <div class="result-list">${result.retrievedEvidence.map(renderEvidence).join("")}</div>
    </div>
    <div class="section-block">
      <h3>Hypotheses</h3>
      <div class="result-list">${result.rootCauseHypotheses.map(renderHypothesis).join("")}</div>
    </div>
    <div class="section-block">
      <h3>Recommended Actions</h3>
      <div class="result-list">${result.recommendedActions.map(renderAction).join("")}</div>
    </div>
    <div class="section-block">
      <h3>Notes</h3>
      <p>${escapeHtml(result.notes || "")}</p>
    </div>
  `;
}

function renderEvidence(item) {
  return `
    <article class="result-item">
      <h3>${escapeHtml(item.source || "evidence")}</h3>
      <p>${escapeHtml(item.summary)}</p>
      <p class="meta">${escapeHtml(item.details || "")}</p>
    </article>
  `;
}

function renderHypothesis(item) {
  return `
    <article class="result-item">
      <h3>${escapeHtml(item.inferenceStrength)} · ${escapeHtml(item.confidence || "unknown")}</h3>
      <p>${escapeHtml(item.description)}</p>
      <div class="badge-row">${(item.evidenceReferences || []).map((value) => `<span class="badge">${escapeHtml(value)}</span>`).join("")}</div>
    </article>
  `;
}

function renderAction(item) {
  return `
    <article class="result-item">
      <h3>${escapeHtml(item.priority)}</h3>
      <p>${escapeHtml(item.description)}</p>
      <p class="meta">${escapeHtml(item.rationale || "")}</p>
      <div class="badge-row">${(item.supportingSignals || []).map((value) => `<span class="badge">${escapeHtml(value)}</span>`).join("")}</div>
    </article>
  `;
}

function renderRagMatch(item) {
  return `
    <article class="result-item">
      <h3>${escapeHtml(item.title)}</h3>
      <p>${escapeHtml(item.summary)}</p>
      <p class="meta">Score ${item.score} · ${escapeHtml(item.sectionPath || "root")}</p>
      <div class="badge-row">${(item.tags || []).slice(0, 8).map((value) => `<span class="badge">${escapeHtml(value)}</span>`).join("")}</div>
    </article>
  `;
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

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

checkHealth();
searchRag();
loadRecent();
