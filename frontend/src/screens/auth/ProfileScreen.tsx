import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import type { AuthError } from "../../auth/authStore";

function logoutMessage(error: AuthError): string {
  if (error.kind === "busy")
    return "Another authentication operation is in progress. Sign-out was not confirmed.";
  if (error.kind === "stale")
    return "Authentication state changed. Please check your session. Sign-out was not confirmed.";
  if (error.kind === "http" && error.status === 401)
    return "Your session is invalid. Server-side sign-out was not confirmed. Please check your session.";
  if (
    error.kind === "http" &&
    error.status === 400 &&
    error.code === "CSRF_VALIDATION_FAILED"
  )
    return "The security check could not be completed. Sign-out was not confirmed. Please check your session.";
  return "Sign-out could not be confirmed. Authentication is currently unavailable. Please check your session before trying again.";
}

export default function ProfileScreen() {
  const { state, actions } = useAuth();
  const nav = useNavigate();
  const [busy, setBusy] = useState(false);
  const [identityHidden, setIdentityHidden] = useState(false);
  const [error, setError] = useState("");
  const submitting = useRef(false);
  const mounted = useRef(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  async function logout() {
    if (submitting.current || state.pending !== null) return;
    submitting.current = true;
    setBusy(true);
    setIdentityHidden(true);
    setError("");
    try {
      const result = await actions.logout();
      if (!mounted.current) return;
      // Accepted AuthApi only returns successful logout for HTTP 204.
      if (result.ok) nav("/login");
      else setError(logoutMessage(result.error));
    } catch {
      if (mounted.current)
        setError(
          "Sign-out could not be confirmed. Authentication is currently unavailable. Please check your session before trying again.",
        );
    } finally {
      submitting.current = false;
      if (mounted.current) setBusy(false);
    }
  }

  const pending = busy || state.pending !== null;
  const session =
    !identityHidden &&
    !pending &&
    (state.status === "AUTHENTICATED" || state.status === "RESTRICTED")
      ? state.session
      : null;
  // Local markup avoids mock/storage initialization through PageHeader/ui/i18n imports.
  // Provider may unmount this component during logout; integration must handle that separately.
  return (
    <>
      <header className="page-header">
        <h1>Profile</h1>
      </header>
      <section className="panel form-panel">
        <span className="eyebrow">STAFF PROFILE</span>
        {session ? (
          <>
            <h2>{session.user.name}</h2>
            <dl className="metadata">
              <div>
                <dt>ID</dt>
                <dd>{session.user.id}</dd>
              </div>
              <div>
                <dt>Username</dt>
                <dd>{session.user.username}</dd>
              </div>
              <div>
                <dt>Session expires</dt>
                <dd>
                  <time dateTime={session.expiresAt}>{session.expiresAt}</time>
                </dd>
              </div>
              <div>
                <dt>Password change required</dt>
                <dd>{session.mustChangePassword ? "Yes" : "No"}</dd>
              </div>
            </dl>
          </>
        ) : (
          <p role="status">
            {busy
              ? "Signing out…"
              : "No confirmed profile is available. Please check your session."}
          </p>
        )}
        {error && <p role="alert">{error}</p>}
        <div className="form-actions">
          {session && (
            <Link className="secondary" to="/change-password">
              Change password
            </Link>
          )}
          {(session || identityHidden) && (
            <button
              className="primary"
              type="button"
              onClick={logout}
              disabled={pending}
              aria-busy={pending}
            >
              {pending ? "Signing out…" : "Logout"}
            </button>
          )}
        </div>
      </section>
    </>
  );
}
