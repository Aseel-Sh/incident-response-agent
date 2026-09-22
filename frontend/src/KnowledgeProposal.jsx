import { useState } from "react";

function ProposalActions({ proposal, canReview, busy, review, onSearch }) {
  if (proposal.status === "pending") {
    if (!canReview) return <p>Awaiting administrator review. Responders can read this draft; only admins can edit and publish it.</p>;
    return <div className="knowledge-actions"><button disabled={busy} onClick={() => review("approved")}>{busy ? "Saving review…" : "Approve and publish"}</button><button disabled={busy} className="secondary danger-outline" onClick={() => review("rejected")}>Reject draft</button></div>;
  }
  if (proposal.status === "rejected") return <p>This rejected update will not be used by RAG or similarity.</p>;
  if (proposal.publicationStatus !== "published") return <p>Approval is recorded, but Markdown publication has not been verified. Check the knowledge folder and RAG source before relying on this document.</p>;
  return <><p>Published to runbook knowledge. Indexing is verified on the next RAG search; a provider outage can delay it.</p><code>approved-{proposal.id.replaceAll("-", "")}.md</code><button className="secondary compact-button" onClick={() => onSearch(proposal.title)}>View published runbook in RAG</button></>;
}

function ProposalEditor({ incident, proposal, canReview, onReview, onSearch }) {
  const [content, setContent] = useState(proposal?.content || "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function review(decision) {
    setBusy(true); setError("");
    try { await onReview(incident.incidentId, decision, content); }
    catch (failure) { setError(failure.message || "The review could not be saved. Retry."); }
    finally { setBusy(false); }
  }
  const pending = proposal.status === "pending";
  return <section className="analysis-card knowledge-card" aria-label="Runbook proposal">
    <div className="knowledge-heading"><h3>Proposed runbook / postmortem update</h3><span className={`badge knowledge-status knowledge-${proposal.status}`}>{proposal.status}</span></div>
    <p>{pending ? "Runbook proposal generated · Pending review" : proposal.status === "rejected" ? "Rejected · Not reusable knowledge" : proposal.publicationStatus === "published" ? "Approved · Markdown published" : "Approved · Publication not verified"}</p>
    {(error || proposal.publicationError) && <p className="error-box" role="alert">{error || proposal.publicationError}</p>}
    <label>Edit proposed update<textarea data-knowledge-content value={content} onChange={event => setContent(event.target.value)} readOnly={!pending || !canReview} /></label>
    <ProposalActions proposal={proposal} canReview={canReview} busy={busy} review={review} onSearch={onSearch} />
  </section>;
}

export default function KnowledgeProposal(props) {
  if (!props.incident.proposedKnowledgeUpdate) return <section className="analysis-card knowledge-card"><h3>Runbook proposal</h3><p>No proposal yet. Resolving this incident generates a draft for review.</p></section>;
  return <ProposalEditor {...props} proposal={props.incident.proposedKnowledgeUpdate} />;
}
