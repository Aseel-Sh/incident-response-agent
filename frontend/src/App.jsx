import { lazy, Suspense, useEffect, useState } from "react";
import { AuthPage, SessionLoading } from "./auth.jsx";
import { getReturnUrl } from "./auth-session.js";

const IncidentConsoleShell = lazy(() => import("./ConsoleShell.jsx"));

export default function App() {
  const [session, setSession] = useState(null);
  const [unavailable, setUnavailable] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    let disposed = false;
    const timeout = window.setTimeout(() => controller.abort(), 10000);
    fetch("/auth/session", { cache: "no-store", signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) throw new Error("Session unavailable");
        const nextSession = await response.json();
        if (disposed) return;
        if (nextSession.authenticated && nextSession.canAccess && location.pathname === "/login" && new URLSearchParams(location.search).get("reason") !== "signed-out") {
          location.replace(getReturnUrl());
          return;
        }
        setSession(nextSession);
      })
      .catch(() => { if (!disposed) setUnavailable(true); })
      .finally(() => window.clearTimeout(timeout));
    // Revalidate a restored page or a session changed in another tab.
    const restore = (event) => { if (event.persisted) location.reload(); };
    const syncSession = (event) => { if (event.key === "incidentops.authChanged") location.replace("/login?reason=signed-out"); };
    window.addEventListener("pageshow", restore);
    window.addEventListener("storage", syncSession);
    return () => {
      disposed = true;
      controller.abort();
      window.clearTimeout(timeout);
      window.removeEventListener("pageshow", restore);
      window.removeEventListener("storage", syncSession);
    };
  }, []);

  if (unavailable) return <AuthPage unavailable />;
  if (!session) return <SessionLoading />;
  if (new URLSearchParams(location.search).get("reason") === "signed-out") return <AuthPage session={{ browserLoginEnabled: session.browserLoginEnabled, authenticated: false }} />;
  if (!session.authenticated || !session.canAccess) return <AuthPage session={session} />;
  return <Suspense fallback={<SessionLoading />}><IncidentConsoleShell /></Suspense>;
}
