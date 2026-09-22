export function createApiClient({ getSession, isSessionEnded, onUnauthorized }) {
  return async function requestJson(url, options = {}) {
    if (isSessionEnded()) throw new Error("Your session has ended. Sign in again to continue.");
    const headers = new Headers(options.headers || {});
    if (getSession().csrfToken && !["GET", "HEAD", "OPTIONS", "TRACE"].includes(String(options.method || "GET").toUpperCase())) headers.set("X-CSRF-TOKEN", getSession().csrfToken);
    let response;
    try { response = await fetch(url, { ...options, headers }); }
    catch (error) {
      if (error.name === "AbortError") throw error;
      throw new Error("The API could not be reached. Check the connection and retry.", { cause: error });
    }
    if (response.status === 401) {
      onUnauthorized();
      throw new Error("Your session has expired. Sign in again to continue.");
    }
    const text = await response.text();
    let data;
    try { data = text ? JSON.parse(text) : null; }
    catch {
      if (response.ok) throw new Error("The API returned an unreadable response. Retry or inspect the server logs.");
    }
    if (!response.ok) {
      const message = data?.detail || data?.title || (data?.analysisState === "failed" ? data.notes : null) || (typeof data === "string" ? data : null);
      throw new Error(message || `Request failed (${response.status} ${response.statusText}). Try again or check service availability.`);
    }
    return data;
  };
}
