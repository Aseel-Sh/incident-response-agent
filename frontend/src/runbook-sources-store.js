let snapshot = { sources: [], loaded: false, error: null, canManage: false, onAction: null };
const listeners = new Set();
export const subscribeSources = listener => { listeners.add(listener); return () => listeners.delete(listener); };
export const getSources = () => snapshot;
export function publishSources(data) { snapshot = { ...snapshot, ...data }; listeners.forEach(listener => listener()); }
