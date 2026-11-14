import { useRef, useState } from "react";
import { Link } from "react-router-dom";
import {
  activationReason,
  unknownActivationMessage,
  type useActivateUser,
} from "./useActivateUser";

function ConfirmationForm({
  disabled,
  activate,
}: {
  disabled: boolean;
  activate(reason: string): Promise<void>;
}) {
  const [draft, setDraft] = useState("");
  const [review, setReview] = useState<string | null>(null);
  const reviewed = useRef<string | null>(null);
  const [error, setError] = useState("");
  function preview() {
    const validated = activationReason(draft);
    if (validated.error) {
      setError(validated.error);
      return;
    }
    setError("");
    reviewed.current = validated.reason;
    setReview(validated.reason);
  }
  function confirm() {
    if (disabled || reviewed.current === null) return;
    const reason = reviewed.current;
    reviewed.current = null;
    setReview(null);
    void activate(reason);
  }
  return (
    <form
      aria-label="Activate user account"
      onSubmit={(e) => e.preventDefault()}
      noValidate
    >
      <fieldset disabled={disabled}>
        <legend>Activation reason</legend>
        <label>
          Reason
          <textarea
            name="reason"
            required
            autoComplete="off"
            value={draft}
            onChange={(e) => {
              setDraft(e.target.value);
              reviewed.current = null;
              setReview(null);
              setError("");
            }}
          />
        </label>
        <button type="button" onClick={preview}>
          Review activation
        </button>
        {review !== null && (
          <section aria-label="Review activation">
            <p>
              Review the target account above and this reason before sending the
              request:
            </p>
            <p>{review}</p>
            <button type="button" onClick={confirm}>
              Confirm activation
            </button>
          </section>
        )}
      </fieldset>
      {error && <p role="alert">{error}</p>}
    </form>
  );
}

export function ActivateUserPage({
  target,
  flow,
}: {
  target: number;
  flow: ReturnType<typeof useActivateUser>;
}) {
  const { snapshot, progress } = flow;
  const locked = ["pending", "unknown", "stale", "confirmed"].includes(
    progress.phase,
  );
  return (
    <main>
      <h1>Activate user account {target}</h1>
      <Link to="/profile">Profile</Link>
      <p>
        The server verifies the account state and activation permission at the
        time of activation. Loading a profile does not grant activation
        permission, including for your own account.
      </p>
      <p>
        Activation does not reset passwords, lockout or credential expiry, and
        does not restore revoked sessions.
      </p>
      {progress.phase === "unknown" && (
        <>
          <p role="alert">{unknownActivationMessage}</p>
          <p>
            A subsequent management GET confirms only profile data and version.
            It does not prove that activation committed. Activation remains
            disabled in this flow.
          </p>
        </>
      )}
      {progress.phase === "confirmed" && (
        <p role="status">
          Activation confirmed by the server. Account ID: {target}.
          accountStatus: ACTIVE.
        </p>
      )}
      {progress.phase === "pending" && (
        <p role="status">Activation pending...</p>
      )}
      {progress.message && (
        <p role="alert">
          {progress.phase === "not-dispatched"
            ? "No activation request was sent. "
            : ""}
          {progress.message}
        </p>
      )}
      {flow.error && <p role="alert">{flow.error}</p>}
      {flow.loading && <p role="status">Loading profile...</p>}
      {snapshot && (
        <section aria-label="Server management snapshot">
          <p>Account ID: {snapshot.profile.id}</p>
          <p>Full name: {snapshot.profile.fullName}</p>
          <p>Email: {snapshot.profile.email}</p>
          {!locked && (
            <ConfirmationForm
              key={snapshot.revision}
              disabled={locked}
              activate={flow.activate}
            />
          )}
        </section>
      )}
      <button
        type="button"
        disabled={flow.loading || progress.phase === "pending"}
        onClick={flow.reload}
      >
        Reload profile
      </button>
    </main>
  );
}
