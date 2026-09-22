import { useState } from "react";
import { getReturnUrl, notifySignOut } from "./auth-session.js";

function AuthLayout({ children }) {
  const [theme, setTheme] = useState(() => document.documentElement.dataset.theme);
  function toggleTheme() {
    const next = theme === "dark" ? "light" : "dark";
    document.documentElement.dataset.theme = next;
    localStorage.setItem("incidentops.theme", next);
    setTheme(next);
  }
  return <div className="auth-layout">
    <header className="auth-header">
      <a className="auth-brand" href="/" aria-label="incidentOps home"><span className="brand-mark" aria-hidden="true">ir</span><strong>incidentOps</strong></a>
      <button className="secondary" onClick={toggleTheme} aria-label="Toggle theme">{theme === "dark" ? "Light theme" : "Dark theme"}</button>
    </header>
    <main className="auth-main" id="login-content">{children}</main>
    <footer className="auth-footer">Incident Response Agent<span>Authorized responders only</span></footer>
  </div>;
}

export function SessionLoading() {
  return <AuthLayout><section className="auth-content" aria-busy="true"><h1>Connecting to your workspace</h1><p role="status">Checking your session…</p></section></AuthLayout>;
}

function SignInContent({ session }) {
  const reason = new URLSearchParams(location.search).get("reason");
  const messages = { expired: "Your session has expired. Sign in again to continue.", "signed-out": "Sign-out was started in another tab. Sign in again to continue.", failed: "Sign-in could not be completed. Please try again.", cancelled: "Sign-in was cancelled. You can try again when you’re ready." };
  return <>
    <h1 id="login-title">Sign in to your workspace</h1>
    <p>Sign in with your account to access incidents, investigations, and response tools.</p>
    {messages[reason] && <p className="auth-notice" role="status">{messages[reason]}</p>}
    <div className="auth-actions">
      {session.browserLoginEnabled ? <a className="auth-primary" href={`/auth/login?returnUrl=${encodeURIComponent(getReturnUrl())}`}>Continue to sign in <span aria-hidden="true">→</span></a>
        : <p className="auth-notice" role="status">Sign-in is unavailable. Contact your administrator to configure workspace authentication.</p>}
    </div>
    {session.browserLoginEnabled && <p className="auth-caption">You’ll continue to the secure sign-in page. No organization membership is required by this app.</p>}
  </>;
}

function AccessDeniedContent({ session }) {
  return <>
    <h1 id="login-title">Workspace access required</h1>
    <p>You’re signed in as {session.name || "an authenticated user"}, but your account doesn’t have responder access. Ask your administrator to grant access, or sign out to use another account.</p>
    {session.browserLoginEnabled && <div className="auth-actions"><form method="post" action="/auth/logout" onSubmit={notifySignOut}><input type="hidden" name="__RequestVerificationToken" value={session.csrfToken || ""} /><button type="submit">Sign out</button></form></div>}
  </>;
}

export function AuthPage({ session, unavailable = false }) {
  let content;
  if (unavailable) content = <><h1 id="login-title">Unable to check your session</h1><p>The authentication service could not be reached. Retry to check your connection.</p><div className="auth-actions"><button onClick={() => location.reload()}>Retry connection</button></div></>;
  else if (session.authenticated && !session.canAccess) content = <AccessDeniedContent session={session} />;
  else content = <SignInContent session={session} />;
  return <AuthLayout><section className="auth-content" aria-labelledby="login-title">{content}</section></AuthLayout>;
}
