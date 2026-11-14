import { useEffect, useRef, useState, type RefObject } from "react";
import { Link, Navigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createUserProfileApi } from "./userProfileApi";
import type { GetProfileResult } from "./userProfileContracts";
import {
  createCredentialResetApi,
  type ResetCredential,
} from "./credentialResetApi";

function ResetFlow({
  target,
  consumed,
  sessionFailure,
}: {
  target: number;
  consumed: RefObject<boolean>;
  sessionFailure(): void;
}) {
  const [api] = useState(() => createCredentialResetApi());
  const [profiles] = useState(() => createUserProfileApi());
  const [password, setPassword] = useState("");
  const [review, setReview] = useState<Extract<
    GetProfileResult,
    { ok: true }
  > | null>(null);
  const [attested, setAttested] = useState(false);
  const [secret, setSecret] = useState<ResetCredential | null>(null);
  const [revealed, setRevealed] = useState(false);
  const [message, setMessage] = useState("");
  const [phase, setPhase] = useState(consumed.current ? "locked" : "idle");
  const mounted = useRef(false),
    busy = useRef(false),
    controller = useRef<AbortController | null>(null);
  const epoch = useRef(0);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      ++epoch.current;
      controller.current?.abort();
    };
  }, []);
  const valid = () => mounted.current && !controller.current?.signal.aborted;
  async function prepare() {
    if (busy.current || consumed.current || !password.trim()) return;
    busy.current = true;
    setPhase("loading");
    setMessage("");
    setReview(null);
    setAttested(false);
    const abort = new AbortController();
    controller.current = abort;
    const supplied = password;
    setPassword("");
    try {
      const proof = await api.reauthenticate(supplied, valid, abort.signal);
      if (!valid()) return;
      if (proof.outcome !== "CONFIRMED") {
        setMessage(
          "Administrator verification was not confirmed. No credential reset was sent.",
        );
        setPhase("idle");
        if (proof.status === 401 && proof.code !== "INVALID_CREDENTIALS")
          sessionFailure();
        return;
      }
      const result = await profiles.getProfile(target, abort.signal);
      if (!valid()) return;
      if (!result.ok) {
        setMessage(
          "The server did not authorize or confirm this account for review. No reset was sent.",
        );
        setPhase("idle");
        if (result.error.status === 401) sessionFailure();
        return;
      }
      setReview(result);
      setPhase("review");
    } finally {
      busy.current = false;
    }
  }
  async function confirm() {
    if (!review || !attested || busy.current || consumed.current) return;
    busy.current = true;
    consumed.current = true;
    const selected = review;
    setReview(null);
    setAttested(false);
    setPhase("saving");
    setMessage("");
    const abort = new AbortController();
    controller.current = abort;
    const result = await api.reset(
      target,
      selected.etag,
      true,
      valid,
      abort.signal,
    );
    if (!valid()) return;
    busy.current = false;
    if (result.outcome === "CONFIRMED") {
      setSecret(result.data);
      setPhase("complete");
      return;
    }
    if (result.outcome === "OUTCOME_UNKNOWN") {
      setPhase("locked");
      setMessage(
        "Reset outcome unknown. The password may have changed and sessions may be revoked. Do not replay. Ask an authorized administrator to verify the account and arrange a separate recovery.",
      );
      return;
    }
    consumed.current = false;
    setPhase("idle");
    setMessage(
      result.status === 412 || result.status === 428
        ? "User version changed. Verify again and review a fresh profile before another confirmation."
        : result.status === 403
          ? "Server denied reset or requires fresh administrator verification. No reset was retried."
          : "Reset was not confirmed. Verify again before a new explicit attempt.",
    );
    if (result.status === 401) sessionFailure();
  }
  return (
    <main>
      <h1>Reset account credential</h1>
      <Link to={`/users/${target}`}>Account detail</Link>
      <p>
        Account ID: {target}. Only the server decides reset authority. Profile
        access is not permission to reset.
      </p>
      {phase === "idle" && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void prepare();
          }}
        >
          <label>
            Current administrator password
            <input
              type="password"
              autoComplete="current-password"
              value={password}
              maxLength={256}
              onChange={(e) => setPassword(e.target.value)}
            />
          </label>
          <button type="submit">Verify administrator and load account</button>
        </form>
      )}
      {(phase === "loading" || phase === "saving") && (
        <p role="status">Checking request...</p>
      )}
      {review && (
        <section aria-label="Review credential reset">
          <p>Full name: {review.data.fullName}</p>
          <p>Email: {review.data.email}</p>
          <p>
            A new temporary password will replace the current credential and
            revoke all target sessions. Roles and branch memberships are
            unchanged.
          </p>
          <label>
            <input
              type="checkbox"
              checked={attested}
              onChange={(e) => setAttested(e.target.checked)}
            />
            I verified this employee's identity using the approved procedure
          </label>
          <button
            type="button"
            disabled={!attested}
            onClick={() => void confirm()}
          >
            Confirm credential reset
          </button>
        </section>
      )}
      {secret && (
        <section aria-label="Reset credential handoff">
          <h2>Credential reset confirmed</h2>
          <p>Account ID: {target}</p>
          <p>Expires at: {secret.expiresAtUtc}</p>
          {revealed ? (
            <p aria-label="Temporary password">{secret.password}</p>
          ) : (
            <p>Password hidden</p>
          )}
          <button type="button" onClick={() => setRevealed((v) => !v)}>
            {revealed ? "Hide password" : "Reveal password"}
          </button>
          <button
            type="button"
            onClick={() => {
              const version = epoch.current;
              void (async () => {
                try {
                  await navigator.clipboard.writeText(secret.password);
                  if (valid() && version === epoch.current)
                    setMessage(
                      "Copied. Delivery to the employee is not confirmed.",
                    );
                } catch {
                  if (valid() && version === epoch.current)
                    setMessage(
                      "Clipboard unavailable. Use a secure manual handoff.",
                    );
                }
              })();
            }}
          >
            Copy password
          </button>
          <p>
            The clipboard can retain this password. Leaving removes access here;
            it cannot be retrieved again.
          </p>
          <button
            type="button"
            onClick={() => {
              ++epoch.current;
              setSecret(null);
              setRevealed(false);
              setMessage(
                "Credential removed. Do not reset the account again to recover this handoff.",
              );
            }}
          >
            I securely handed off the credential
          </button>
        </section>
      )}
      {message && <p role="alert">{message}</p>}
    </main>
  );
}

/** Additive route for the existing active backend; no permission or proof inferred on the client. */
export function CredentialResetRoute() {
  const { id } = useParams();
  const { state, actions } = useAuth();
  const target =
    id && /^[1-9][0-9]*$/.test(id) && Number.isSafeInteger(Number(id))
      ? Number(id)
      : null;
  const consumed = useRef(false),
    mounted = useRef(false),
    recovering = useRef(false);
  const [blocked, setBlocked] = useState(false);
  const owner = useRef(state.session),
    revision = useRef(0);
  if (owner.current !== state.session) {
    owner.current = state.session;
    ++revision.current;
  }
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate replace to="/login" />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (
    blocked ||
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null
  )
    return (
      <main aria-label="Reset session recovery">
        <p>
          Account data and credentials are hidden. No reset has been retried.
        </p>
        <button
          type="button"
          onClick={() => {
            if (recovering.current) return;
            recovering.current = true;
            void actions
              .revalidate()
              .then((result) => {
                if (mounted.current && result.ok) setBlocked(false);
              })
              .finally(() => {
                recovering.current = false;
              });
          }}
        >
          Check session
        </button>
      </main>
    );
  if (!target) return <p role="alert">Invalid user ID</p>;
  if (String(target) === state.session.user.id)
    return (
      <p role="alert">
        Use Change Password for your own account. Self reset is unavailable.
      </p>
    );
  return (
    <ResetFlow
      key={revision.current + ":" + target}
      target={target}
      consumed={consumed}
      sessionFailure={() => setBlocked(true)}
    />
  );
}
