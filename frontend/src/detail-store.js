let snapshot = null;
const listeners = new Set();
export const subscribeDetail = listener => { listeners.add(listener); return () => listeners.delete(listener); };
export const getDetail = () => snapshot;
export function publishDetail(data) { snapshot = data; listeners.forEach(listener => listener()); }
