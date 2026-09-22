import DashboardOverview from "./DashboardOverview.jsx";
import RunbookSources from "./RunbookSources.jsx";

export default function ConsoleLayout() {
 return <>
<a className="skip-link" href="#workspace">{"Skip to workspace"}</a>
<div className="app-shell">
    <aside className="sidebar">
      <div className="brand">
        <span className="brand-mark" aria-hidden="true">{"ir"}</span>
        <strong>{"incidentOps"}</strong>
      </div>
      <nav className="nav-tabs" aria-label="Primary">
        <span className="nav-section">{"Respond"}</span>
        <button className="nav-tab active" type="button" data-tab="dashboard" data-icon="queue" aria-current="page">{"Incidents"}</button>
        <button className="nav-tab" type="button" data-tab="analysis" data-icon="search">{"Analysis"}</button>
        <button className="nav-tab" type="button" data-tab="history" data-icon="history">{"History"}</button>
        <span className="nav-section">{"Operate"}</span>
        <button className="nav-tab" type="button" data-tab="sources" data-icon="plug">{"Sources"}</button>
        <button className="nav-tab" type="button" data-tab="ingestion" data-icon="terminal">{"Diagnostics"}</button>
        <button className="nav-tab" type="button" data-tab="rag" data-icon="book">{"RAG"}</button>
        <button className="nav-tab" type="button" data-tab="evaluation" data-icon="check">{"Evaluation"}</button>
        <button className="nav-tab" type="button" data-tab="monitor" data-icon="activity">{"Monitor"}</button>
        <button className="nav-tab" type="button" data-tab="config" data-icon="database">{"Config"}</button>
      </nav>
      <div className="sidebar-footer">
        <div id="accountSummary" className="account-summary"></div>
        <span id="appVersion">{"v..."}</span>
        <span id="sidebarLastScan">{"Last scan: not run"}</span>
      </div>
    </aside>

    <main className="workspace" id="workspace" tabIndex="-1">
      <header className="topbar">
        <div>
          <h1 id="workspaceTitle">{"Incident queue"}</h1>
          <p id="workspaceEyebrow">{"Triage signals. Coordinate the response."}</p>
        </div>
        <div className="topbar-actions">
          <label className="project-selector" aria-label="Project workspace">
            <span data-icon="database">{"Project"}</span>
            <select id="projectSelector"></select>
          </label>
          <span id="healthStatus" className="status-pill status-pending" title="API process health. Provider availability is reported in Configuration and analysis diagnostics.">{"Checking"}</span>
          <span id="demoModePill" className="status-pill status-warning">{"Demo Mode"}</span>
          <button id="scanButton" className="top-icon" type="button" data-icon="moon" aria-label="Toggle theme"></button>
        </div>
      </header>

      <section id="dashboardView" className="tab-view active">
        <div id="sourceModeBanner" className="mode-banner"></div>
        <DashboardOverview />
        <section className="panel">
          <div className="toolbar backlog-toolbar">
            <label className="search-field" aria-label="Search incidents">
              <span data-icon="search"></span>
              <input id="incidentSearch" type="search" aria-label="Search incidents" placeholder="Search incidents, services, owners…" />
            </label>
            <div id="statusFilters" className="chip-row">
              <button className="filter-chip active" type="button" data-status="all" aria-pressed="true">{"Open queue"}</button>
              <button className="filter-chip" type="button" data-status="candidate">{"Candidates"}</button>
              <button className="filter-chip" type="button" data-status="new">{"Confirmed"}</button>
              <button className="filter-chip" type="button" data-status="active">{"Active"}</button>
              <button className="filter-chip" type="button" data-status="mitigated">{"Mitigated"}</button>
              <button className="filter-chip" type="button" data-status="resolved">{"Resolved"}</button>
            </div>
            <button id="manualIncidentButton" className="secondary compact-button" type="button" data-icon="plus">{"Create Incident"}</button>
          </div>
          <div id="detectedOutput" className="incident-backlog"></div>
        </section>
      </section>

      <section id="analysisView" className="tab-view">
        <div className="page-heading compact-only">
          <h2>{"Incident analysis"}</h2>
          <p>{"Evidence, runbook steps, and related incidents for the selected signal."}</p>
        </div>
        <div className="analysis-layout">
          <section className="panel compact-panel">
            <div className="section-title">
              <h2>{"Incident input"}</h2>
              <button id="sampleButton" className="secondary compact-button" type="button" data-icon="copy">{"Sample"}</button>
            </div>
            <form id="incidentForm" className="form-grid">
              <label className="wide">{"Title"}<input name="title" required maxLength="200" placeholder="Describe the incident" /></label>
              <label>{"Severity"}<select name="severity" required defaultValue=""><option value="" disabled>{"Select severity"}</option><option value="sev1">{"SEV-1 — Critical"}</option><option value="sev2">{"SEV-2 — Major"}</option><option value="sev3">{"SEV-3 — Partial"}</option><option value="sev4">{"SEV-4 — Minor"}</option><option value="sev5">{"SEV-5 — Informational"}</option></select></label>
              <label>{"Service"}<input name="serviceName" placeholder="e.g. checkout-service" /></label>
              <label>{"Environment"}<input name="environment" placeholder="e.g. prod" /></label>
              <label>{"Session ID"}<input name="sessionId" placeholder="optional follow-up session" /></label>
              <label className="wide">{"Tags"}<input name="tags" placeholder="comma-separated tags" /></label>
              <label className="wide">{"Description"}<textarea name="description" required maxLength="5000" placeholder="What is happening and who is affected?"></textarea></label>
              <div className="actions wide">
                <button id="analyzeButton" type="submit" data-icon="play">{"Create candidate & confirm"}</button>
              </div>
            </form>
          </section>
          <section className="analysis-feed">
            <div id="analysisStatus" className="analysis-status" role="status">{"Select a backlog incident or run manual analysis."}</div>
            <div id="analysisOutput" className="empty-state">{"No analysis yet."}</div>
          </section>
        </div>
      </section>

      <section id="historyView" className="tab-view">
        <div className="page-heading history-heading">
          <div><h2><span data-icon="history"></span>{"Incident History"}</h2><p><span id="historyResultCount">{"0 results"}</span><span className="history-count-divider" aria-hidden="true">{"·"}</span><span id="historyTotal">{"0"}</span>{" total incidents"}</p></div>
          <button id="historyReloadButton" className="icon-refresh-button" type="button" data-icon="refresh" aria-label="Refresh history"></button>
        </div>
        <div className="section-title history-filters">
          <div className="filter-icon" data-icon="search"></div>
          <input id="historySearch" type="search" aria-label="Search history" placeholder="Search history..." />
          <select id="historyServiceFilter" aria-label="Service filter"><option value="all">{"All services"}</option></select>
          <select id="historyStatusFilter" aria-label="Status filter"><option value="all">{"All statuses"}</option></select>
          <select id="historySessionFilter" aria-label="Session filter"><option value="all">{"All sessions"}</option></select>
          <select id="historySeverityFilter" aria-label="Severity filter"><option value="all">{"All severities"}</option></select>
        </div>
        <section className="panel">
          <div className="history-layout">
            <div id="recentOutput" className="history-list"></div>
          </div>
        </section>
      </section>

      <section id="sourcesView" className="tab-view">
        <div className="page-heading">
          <h2><span data-icon="database"></span>{"Sources"}</h2>
          <p>{"Configured data sources and their capabilities"}</p>
        </div>
        <details className="panel project-manager-panel setup-disclosure">
          <summary>{"Add a project workspace"}</summary>
          <div className="section-title">
            <div>
              <h3><span data-icon="database"></span>{"Project workspaces"}</h3>
              <p>{"Add a service/project without editing appsettings. The monitor will scan its logs, metrics, health endpoint, and thresholds."}</p>
            </div>
          </div>
          <form id="projectForm" className="form-grid project-form">
            <label>{"Project ID"}<input name="id" required placeholder="project-a" /></label>
            <label>{"Name"}<input name="name" required placeholder="Project A" /></label>
            <label className="wide">{"Log file path"}<input name="logEntriesPath" placeholder="C:\\path\\to\\logs.json" /></label>
            <label className="wide">{"Metric file path"}<input name="metricSamplesPath" placeholder="C:\\path\\to\\metrics.json" /></label>
            <label className="wide">{"Health endpoint"}<input name="sourceHealthEndpoint" placeholder="http://127.0.0.1:5268/health" /></label>
            <label>{"Error threshold"}<input name="highErrorRateThreshold" type="number" step="0.1" placeholder="25" /></label>
            <label>{"Latency threshold ms"}<input name="latencyWarningThresholdMs" type="number" step="1" placeholder="1000" /></label>
            <div className="actions wide">
              <button type="submit" data-icon="plug">{"Add project"}</button>
            </div>
          </form>
        </details>
        <section className="panel">
          <div id="sourcesOutput" className="source-list"></div>
        </section>
      </section>

      <section id="ingestionView" className="tab-view">
        <div className="page-heading">
          <h2><span data-icon="activity"></span>{"Signal Diagnostics"}</h2>
          <p>{"Manual ingestion for connector testing, emergency input, and detection-rule validation"}</p>
        </div>
        <div className="mode-banner"><div className="mode-banner-content"><span data-icon="info"></span><span>{"Signals submitted here are persisted into the configured log and metric stores and enter the same server-side detection pipeline as connected telemetry. This diagnostics tool is not a replacement for production collectors."}</span></div></div>
        <div className="split-layout">
          <section className="panel">
            <div className="section-title"><h2><span data-icon="terminal"></span>{"POST LOG SIGNAL"}</h2></div>
            <form id="logSignalForm" className="form-grid">
              <label className="wide">{"Service"}<input name="source" defaultValue="checkout-service" required /></label>
              <label className="wide">{"Level"}<select name="level"><option>{"Error"}</option><option>{"Warning"}</option><option>{"Information"}</option><option>{"Critical"}</option></select></label>
              <label className="wide">{"Message"}<textarea name="message" required defaultValue="ERROR: connection pool timeout after 30s…" /></label>
              <div className="form-actions wide">
                <button type="submit" data-icon="terminal">{"Post Signal"}</button>
                <button className="secondary rescan-button" type="button" data-rescan>{"Rescan"}</button>
              </div>
            </form>
          </section>
          <section className="panel">
            <div className="section-title"><h2><span data-icon="chart"></span>{"POST METRIC SAMPLE"}</h2></div>
            <form id="metricSignalForm" className="form-grid">
              <label className="wide">{"Service"}<input name="serviceName" defaultValue="checkout-service" required /></label>
              <label>{"Metric name"}<input name="metricName" defaultValue="p95_latency" required /></label>
              <label>{"Value"}<input name="value" type="number" step="0.1" defaultValue="4200" required /></label>
              <label className="wide">{"Unit (optional)"}<input name="unit" defaultValue="ms" /></label>
              <input type="hidden" name="environment" defaultValue="prod" />
              <div className="form-actions wide">
                <button type="submit" data-icon="chart">{"Post Metric"}</button>
                <button className="secondary rescan-button" type="button" data-rescan>{"Rescan"}</button>
              </div>
            </form>
          </section>
        </div>
        <div id="ingestionFeedback" className="scan-feedback" role="status"></div>
      </section>

      <section id="ragView" className="tab-view">
        <div className="page-heading">
          <h2><span data-icon="book"></span>{"RAG Diagnostics"}</h2>
          <p>{"Embedding provider, vector store, and retrieval match quality"}</p>
        </div>
        <section className="panel runbook-sources-panel">
          <div className="section-title"><div><h3>{"Connected runbook sources"}</h3><p>{"Connect a Markdown directory or an existing local Git working tree. Credentials stay outside the app."}</p></div></div>
          <details className="setup-disclosure"><summary>{"Connect a runbook source"}</summary>
          <form id="runbookSourceForm" className="source-connect-form">
            <label>{"Name"}<input name="name" required placeholder="Operations runbooks" /></label>
            <label>{"Type"}<select name="type"><option value="directory">{"Local / mounted directory"}</option><option value="git">{"Git working tree"}</option></select></label>
            <label className="wide">{"Directory path"}<input name="path" required placeholder="C:\\\\ops\\\\runbooks" /></label>
            <button type="submit" data-icon="plug">{"Connect source"}</button>
          </form>
          </details>
          <RunbookSources />
        </section>
        <section className="panel">
          <div id="ragSummary" className="rag-summary"></div>
          <form id="ragForm" className="search-bar"><input name="query" defaultValue="checkout 5xx latency" aria-label="RAG query" /><button type="submit" data-icon="search">{"Search"}</button></form>
          <div id="ragResults" className="result-list"></div>
        </section>
      </section>

      <section id="evaluationView" className="tab-view">
        <div className="page-heading">
          <h2><span data-icon="check"></span>{"Evaluation"}</h2>
          <p>{"Run scenarios and compare output against rubric items"}</p>
        </div>
        <section className="panel">
          <div id="evaluationOutput" className="evaluation-grid"></div>
        </section>
      </section>

      <section id="monitorView" className="tab-view">
        <div className="page-heading">
          <h2><span data-icon="activity"></span>{"Live Monitor"}</h2>
          <p>{"Polling interval, scan controls, and last scan result"}</p>
        </div>
        <section className="panel">
          <div id="lastScan" className="monitor-summary">{"No scan yet"}</div>
          <div id="scanFeedback" className="scan-feedback"></div>
          <div className="monitor-controls">
            <div className="section-title stacked">
              <h3>{"Controls"}</h3>
              <span className="meta">{"Polling interval: "}<strong id="pollingIntervalValue">{"30s"}</strong></span>
            </div>
            <input id="pollingIntervalSlider" className="polling-slider" type="range" min="10" max="300" step="10" defaultValue="30" aria-label="Polling interval" />
            <div className="slider-ticks" aria-hidden="true"><span style={{"--tick-position": "0%"}}>{"10s"}</span><span style={{"--tick-position": "17.24%"}}>{"60s"}</span><span style={{"--tick-position": "37.93%"}}>{"120s"}</span><span style={{"--tick-position": "100%"}}>{"300s"}</span></div>
            <div className="monitor-actions">
              <button id="pauseScanButton" className="warning-button compact-button" type="button" data-icon="pause">{"Pause Scanning"}</button>
              <button id="manualRefreshButton" className="compact-button" type="button" data-icon="refresh">{"Manual Refresh"}</button>
            </div>
          </div>
        </section>
      </section>

      <section id="configView" className="tab-view">
        <div className="page-heading">
          <h2><span data-icon="database"></span>{"Configuration"}</h2>
          <p>{"Current environment, provider, and mode summary"}</p>
        </div>
        <section className="panel">
          <div id="configOutput" className="config-grid"></div>
        </section>
      </section>
    </main>
  </div>
  <button className="help-button" type="button" aria-label="Help">{"Help"}</button>
  <div id="toastRegion" className="toast-region" aria-live="polite" aria-atomic="true"></div>
  <div id="historyModal" className="modal-backdrop" hidden>
    <section className="modal-panel" role="dialog" aria-modal="true" aria-labelledby="historyModalTitle">
      <button id="historyModalClose" className="modal-close" type="button" aria-label="Close history detail">{"Close "}<span aria-hidden="true">{"×"}</span></button>
      <div id="historyDetail" className="history-detail"><p className="eyebrow">{"Selected run"}</p><h3 id="historyModalTitle">{"No analysis selected"}</h3><p className="meta">{"Choose a saved run to inspect evidence, actions, and provider mode."}</p></div>
    </section>
  </div>
  <div id="confirmModal" className="modal-backdrop confirm-backdrop" hidden>
    <section className="modal-panel confirm-panel" role="alertdialog" aria-modal="true" aria-labelledby="confirmModalTitle" aria-describedby="confirmModalMessage">
      <div className="confirm-icon" data-icon="alert"></div>
      <h2 id="confirmModalTitle">{"Confirm action"}</h2>
      <p id="confirmModalMessage"></p>
      <div className="confirm-actions">
        <button id="confirmModalCancel" className="secondary" type="button">{"Cancel"}</button>
        <button id="confirmModalAccept" className="danger-button" type="button">{"Confirm"}</button>
      </div>
    </section>
  </div>

</>;
}
