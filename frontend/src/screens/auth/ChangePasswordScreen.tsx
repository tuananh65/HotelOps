import { useEffect, useRef, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import type { AuthError } from "../../auth/authStore";

function unmetPasswordItems(current: string, next: string): string[] {
  // Match server UTF-16 char categories; advisory feedback never grants success.
  const units = next.split("");
  const items: string[] = [];
  if (next.length < 8 || next.length > 256) items.push("Use 8 to 256 characters.");
  if (!units.some(c => /\p{Lu}/u.test(c))) items.push("Add an uppercase letter.");
  if (!units.some(c => /\p{Ll}/u.test(c))) items.push("Add a lowercase letter.");
  if (!units.some(c => /\p{Nd}/u.test(c))) items.push("Add a digit.");
  if (!units.some(c => /[\p{P}\p{S}]/u.test(c))) items.push("Add a special character.");
  if (units.some(c => /\p{Cc}/u.test(c))) items.push("Remove control characters.");
  if (current === next) items.push("Choose a password different from your current password.");
  return items;
}

function changePasswordMessage(error: AuthError): string {
  if (error.kind === "busy")
    return "Another authentication operation is in progress. Please wait before trying again.";
  if (error.kind === "stale")
    return "Authentication state changed. Password change was not confirmed. Please check your session.";
  if (error.kind === "http") {
    if (error.status === 400 && error.code === "CURRENT_PASSWORD_INVALID")
      return "The current password is incorrect.";
    if (error.status === 400 && error.code === "PASSWORD_POLICY_VIOLATION")
      return "The new password does not meet the server password policy. Use 8–256 characters with uppercase, lowercase, a digit and a special character; no control characters; choose a password different from your current password.";
    if (error.status === 400 && error.code === "VALIDATION_FAILED")
      return "Please check all password fields and confirmation.";
    if (error.status === 400 && error.code === "CSRF_VALIDATION_FAILED")
      return "The security check could not be completed. Password change was not confirmed. Please check your session.";
    if (error.status === 401)
      return "Your session is no longer valid. Please sign in again before changing your password.";
    if (error.status === 409 && error.code === "AUTH_STATE_CHANGED")
      return "Authentication state changed. Password change was not confirmed. Please check your session.";
  }
  return "Password change could not be confirmed. Authentication is currently unavailable. Please check your session before trying again.";
}

export default function ChangePasswordScreen() {
  const { state, actions } = useAuth();
  const navigate = useNavigate();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const mounted = useRef(false);
  const confirmed =
    state.session !== null &&
    (state.status === "AUTHENTICATED" || state.status === "RESTRICTED");
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting.current || state.pending !== null || !confirmed) return;
    if (
      !currentPassword.length ||
      !newPassword.length ||
      !confirmNewPassword.length
    ) {
      setError("All password fields are required.");
      return;
    }
    if (newPassword !== confirmNewPassword) {
      setError("The new password and confirmation must match.");
      return;
    }
    submitting.current = true;
    setBusy(true);
    setError("");
    try {
      const result = await actions.changePassword({
        currentPassword,
        newPassword,
        confirmNewPassword,
      });
      if (!mounted.current) return;
      if (result.ok) navigate("/profile", { state: { passwordChanged: true } });
      else {
        const details = result.error.kind === "http" && result.error.status === 400
          && result.error.code === "PASSWORD_POLICY_VIOLATION"
          ? unmetPasswordItems(currentPassword, newPassword).join(" ") : "";
        setError([changePasswordMessage(result.error), details].filter(Boolean).join(" "));
      }
    } catch {
      if (mounted.current)
        setError(
          "Password change could not be confirmed. Authentication is currently unavailable. Please check your session before trying again.",
        );
    } finally {
      submitting.current = false;
      if (mounted.current) {
        setBusy(false);
        setCurrentPassword("");
        setNewPassword("");
        setConfirmNewPassword("");
      }
    }
  }

  const pending = busy || state.pending !== null;
  const disabled = pending || !confirmed;
  // Local markup avoids the shared Form/AuthLayout/i18n mock-storage initialization.
  // Provider mutation unmount/recovery is an integration dependency, not solved here.
  return (
    <div className="auth">
      <aside className="auth-art">
        <h1>
          Hotel operations.
          <br />
          One connected workspace.
        </h1>
      </aside>
      <main className="auth-panel">
        <h1>Change Password</h1>
        <p>Use 8–256 characters with uppercase, lowercase, a digit and a special character. The password must differ from your current password and contain no control characters. Requirements are checked by the server.</p>
        {!confirmed && (
          <p role="status">
            A confirmed session is required to change your password.
          </p>
        )}
        <form noValidate onSubmit={submit} aria-busy={pending}>
          <div className="form-grid">
            <label htmlFor="change-current">
              Current password
              <input
                id="change-current"
                name="currentPassword"
                type="password"
                autoComplete="current-password"
                required
                aria-describedby={error ? "change-password-error" : undefined}
                value={currentPassword}
                onChange={(event) => setCurrentPassword(event.target.value)}
                disabled={disabled}
              />
            </label>
            <label htmlFor="change-new">
              New password
              <input
                id="change-new"
                name="newPassword"
                type="password"
                autoComplete="new-password"
                required
                aria-describedby={error ? "change-password-error" : undefined}
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                disabled={disabled}
              />
            </label>
            <label htmlFor="change-confirm">
              Confirm new password
              <input
                id="change-confirm"
                name="confirmNewPassword"
                type="password"
                autoComplete="new-password"
                required
                aria-describedby={error ? "change-password-error" : undefined}
                value={confirmNewPassword}
                onChange={(event) => setConfirmNewPassword(event.target.value)}
                disabled={disabled}
              />
            </label>
          </div>
          {error && (
            <p role="alert" id="change-password-error">
              {error}
            </p>
          )}
          <div className="form-actions">
            <button className="primary" type="submit" disabled={disabled}>
              {pending ? "Changing password…" : "Change password"}
            </button>
          </div>
        </form>
      </main>
    </div>
  );
}
