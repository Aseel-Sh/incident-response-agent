import { useSyncExternalStore } from "react";
import { getDashboard, subscribeDashboard } from "./dashboard-store.js";
import { incidentAnalytics } from "./analytics.js";
import { formatSeverityLabel, formatStatusLabel } from "./presentation.js";

function Distribution({ title, values, label, kind }) {
  const maximum = Math.max(1, ...values.map(([, count]) => count));
  return <section className="distribution"><h3>{title}</h3><ul aria-label={title}>
    {values.map(([key, count]) => <li key={key}>
      <span>{label(key)}</span><span className={`distribution-track lifecycle-${kind === "status" ? key : "neutral"}`} aria-hidden="true"><span style={{ width: `${count / maximum * 100}%` }} /></span><strong>{count}</strong>
    </li>)}
  </ul></section>;
}

export default function DashboardOverview() {
  const { rows, incidents, loaded, project, openIncident } = useSyncExternalStore(subscribeDashboard, getDashboard);
  const analytics = incidentAnalytics(incidents);
  const pending = incidents.filter(item => item.proposedKnowledgeUpdate?.status === "pending");
  return <>
    <dl className="metric-strip dashboard-stats">
      {[["candidate", "Candidates", "newCount"], ["new", "Confirmed", "confirmedCount"], ["active", "Investigating", "investigatingCount"], ["mitigated", "Mitigated", "mitigatedCount"], ["resolved", "Resolved", "resolvedCount"]].map(([status, label, id]) => <div className={`lifecycle-${status}`} key={status}><dt>{label}</dt><dd id={id}>{loaded ? rows.filter(row => row.statusKey === status).length : "—"}</dd></div>)}
    </dl>
    {pending.length > 0 && <section className="learning-review-banner" aria-label="Pending runbook proposals"><div><strong>{pending.length} runbook proposal{pending.length === 1 ? "" : "s"} · Pending review</strong><p>Review drafts before they become reusable knowledge.</p></div><div>{pending.map(item => <button key={item.incidentId} className="secondary compact-button" onClick={() => openIncident(item)}>Review {item.incidentTitle || item.incidentSummary}</button>)}</div></section>}
    <details className="operational-analytics" open>
      <summary>Incident overview</summary>
      <p className="meta">{project} · Latest {incidents.length} saved incidents (up to 100). Candidates excluded. Creation dates use UTC.</p>
      {!loaded ? <p role="status">Loading incident data…</p> : !incidents.length ? <p>No saved incidents yet. Confirm a candidate to see incident trends.</p> : <div className="analytics-grid">
        <Distribution title="Incidents by creation date" values={analytics.days} label={value => value} kind="date" />
        <Distribution title="Severity distribution" values={analytics.severity} label={formatSeverityLabel} kind="severity" />
        <Distribution title="Current lifecycle" values={analytics.status} label={formatStatusLabel} kind="status" />
      </div>}
      {analytics.missingTimestamp > 0 && <p className="meta">{analytics.missingTimestamp} incident(s) have no valid creation timestamp and are excluded from the date chart.</p>}
    </details>
  </>;
}
