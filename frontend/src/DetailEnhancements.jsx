import { useSyncExternalStore } from "react";
import { createPortal } from "react-dom";
import { getDetail, subscribeDetail } from "./detail-store.js";
import KnowledgeProposal from "./KnowledgeProposal.jsx";

export default function DetailEnhancements() {
  const detail = useSyncExternalStore(subscribeDetail, getDetail);
  if (!detail) return null;
  return createPortal(<KnowledgeProposal key={`${detail.incident.incidentId}:${detail.incident.proposedKnowledgeUpdate?.status}:${detail.incident.proposedKnowledgeUpdate?.reviewedAtUtc}`} {...detail} />, detail.target);
}
