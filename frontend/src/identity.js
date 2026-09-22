export function displayIdentity(identity, directory = []) {
  if (!identity) return "Unassigned";
  const profile = directory.find(person => person.id === identity || person.auditName === identity);
  const name = profile?.name || identity;
  if (typeof name !== "string" || name.includes("@") || name.includes("|") || name.startsWith("development:")) return "Responder";
  return name.trim() || "Responder";
}
