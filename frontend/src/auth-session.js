export function getReturnUrl() {
  const value = new URLSearchParams(location.search).get("returnUrl");
  const tabs = ["dashboard", "analysis", "history", "sources", "ingestion", "rag", "evaluation", "monitor", "config"];
  const hash = value?.startsWith("/#") ? value.slice(2) : location.hash.slice(1);
  return tabs.includes(hash) ? `/#${hash}` : "/";
}

export function notifySignOut() {
  localStorage.setItem("incidentops.authChanged", String(Date.now()));
}
