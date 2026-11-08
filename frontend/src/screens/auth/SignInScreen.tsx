import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import type { AuthError } from "../../auth/authStore";

function loginMessage(error: AuthError): string {
  if (error.kind === "busy")
    return "Another authentication operation is in progress. Please wait before trying again.";
  if (error.kind === "stale")
    return "Authentication state changed. Please check your session before trying again.";
  if (error.kind === "http") {
    if (error.status === 401 && error.code === "INVALID_CREDENTIALS")
      return "Invalid username or password.";
    if (error.status === 423 && error.code === "ACCOUNT_LOCKED")
      return "Your account is locked. Please contact your Administrator.";
    if (error.status === 403 && error.code === "TEMPORARY_PASSWORD_EXPIRED")
      return "Your temporary password has expired. Please contact your Administrator.";
    if (error.status === 409 && error.code === "AUTHENTICATED_SESSION_EXISTS")
      return "A signed-in session already exists. Please check your session before trying again.";
    if (error.status === 400 && error.code === "VALIDATION_FAILED")
      return "Please check your username and password and submit again.";
    if (error.status === 400 && error.code === "CSRF_VALIDATION_FAILED")
      return "The security check could not be completed. Please check your session before trying again.";
  }
  return "Authentication is currently unavailable. The sign-in result could not be confirmed. Please try again later.";
}

export default function SignInScreen() {
  const { state, actions } = useAuth();
  const navigate = useNavigate();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const mounted = useRef(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting.current || state.pending !== null) return;
    if (!username.trim() || password.length === 0) {
      setError("Username and password are required.");
      return;
    }
    submitting.current = true;
    setBusy(true);
    setError("");
    try {
      const result = await actions.login({
        username: username.trim(),
        password,
      });
      if (!mounted.current) return;
      if (result.ok)
        navigate(
          result.data.mustChangePassword ? "/change-password" : "/dashboard",
        );
      else setError(loginMessage(result.error));
    } catch {
      // Never render raw exceptions or submitted values, including from a rejected adapter.
      if (mounted.current)
        setError(
          "Authentication is currently unavailable. The sign-in result could not be confirmed. Please try again later.",
        );
    } finally {
      submitting.current = false;
      if (mounted.current) {
        setBusy(false);
        setPassword("");
      }
    }
  }

  const pending = busy || state.pending !== null;
  // Local markup avoids AuthLayout -> ui/services and i18n storage initialization.
  // Shared layout integration is deferred; this component requires an explicit AuthProvider.
  return (
    <div className="auth">
      <aside className="auth-art">
        <span className="eyebrow">DUC VUONG SAIGON HOTEL</span>
        <h1>
          Hotel operations.
          <br />
          One connected workspace.
        </h1>
        <small>BUI VIEN / CAO THANG</small>
      </aside>
      <main className="auth-panel">
        <Link to="/login" className="auth-wordmark">
          HOTEL<span>OPS</span>
        </Link>
        <span className="eyebrow">STAFF PORTAL</span>
        <h1>Sign In</h1>
        <p>Welcome back. Sign in to your hotel workspace.</p>
        <form noValidate onSubmit={submit} aria-busy={pending}>
          <div className="form-grid">
            <label htmlFor="login-username">
              Username or email
              <input
                id="login-username"
                name="username"
                autoComplete="username"
                aria-describedby={error ? "login-error" : undefined}
                required
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                disabled={pending}
              />
            </label>
            <label htmlFor="login-password">
              Password
              <input
                id="login-password"
                name="password"
                type="password"
                autoComplete="current-password"
                aria-describedby={error ? "login-error" : undefined}
                required
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                disabled={pending}
              />
            </label>
          </div>
          {error && (
            <p role="alert" id="login-error">
              {error}
            </p>
          )}
          <div className="form-actions">
            <button className="primary" type="submit" disabled={pending}>
              {pending ? "Signing in…" : "Sign in"}
            </button>
          </div>
        </form>
        <Link className="forgot" to="/forgot-password">
          Forgot password?
        </Link>
      </main>
    </div>
  );
}
