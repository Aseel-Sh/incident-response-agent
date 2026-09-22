// Transitional boundary: controller supplies immutable data; React owns dashboard rendering.
let snapshot = { rows: [], incidents: [], project: "", openIncident: null, loaded: false };
const listeners = new Set();
export const subscribeDashboard = listener => { listeners.add(listener); return () => listeners.delete(listener); };
export const getDashboard = () => snapshot;
export function publishDashboard(data) {
  snapshot = { ...data, loaded: true };
  listeners.forEach(listener => listener());
}
