import { useState, useSyncExternalStore } from "react";
import { getSources, subscribeSources } from "./runbook-sources-store.js";
import { formatTimestamp } from "./presentation.js";

function SourceActions({ source, onAction }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  async function run(action) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try { await onAction(source, action); }
    catch (failure) { setError(failure.message || "Source update failed. Try again."); }
    finally { setBusy(false); }
  }
  return <div className="runbook-source-actions" aria-busy={busy}>
    <button className="secondary compact-button" disabled={busy} data-runbook-source-action="sync" data-source-id={source.id} onClick={() => run("sync")}>Sync</button>
    {source.removable && <>
      <button className="secondary compact-button" disabled={busy} data-runbook-source-action="toggle" data-source-id={source.id} onClick={() => run("toggle")}>{source.enabled ? "Disable" : "Enable"}</button>
      <button className="secondary compact-button danger-outline" disabled={busy} data-runbook-source-action="remove" data-source-id={source.id} onClick={() => run("remove")}>Remove</button>
    </>}
    {error && <p className="error-box" role="alert">{error}</p>}
  </div>;
}

export default function RunbookSources() {
  const { sources, loaded, error, canManage, onAction } = useSyncExternalStore(subscribeSources, getSources, getSources);
  return <div id="runbookSources" className="runbook-source-list">
    {error ? <div className="error-box" role="alert">{error}</div> : !loaded ? <p>Loading runbook sources…</p> : !sources.length ? <div className="empty-state">No runbook sources are connected.</div> : sources.map(source =>
      <article className="runbook-source-card" key={source.id}>
        <div>
          <div className="badge-row"><strong>{source.name}</strong><span className="badge">{source.type}</span><span className={`badge ${source.reachable ? "status-connected" : "status-missing"}`}>{source.reachable ? "Reachable" : "Unavailable"}</span><span className="badge">{source.enabled ? "Enabled" : "Disabled"}</span></div>
          <p>{source.path}</p>
          <small>{Number(source.documentCount) || 0} documents · {Number(source.sectionCount) || 0} source sections · Last synchronized {source.lastSynchronizedAtUtc ? formatTimestamp(source.lastSynchronizedAtUtc) : "not yet"}</small>
          {source.lastError && <div className="error-box compact-error">{source.lastError}</div>}
        </div>
        {canManage && <SourceActions source={source} onAction={onAction} />}
      </article>
    )}
  </div>;
}
