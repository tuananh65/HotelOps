import {
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import { useAuth } from "../auth/AuthProvider";
import { decodeUserDeactivationPreview, UserDeactivationReview } from "./UserDeactivationReview";
import {
  createUserDeactivationCandidateApi,
  type DeactivationResult,
} from "./userDeactivationCandidateApi";

// Deactivate-only, same-origin page lifetime. No credentials, reason, profile or ETag.
// A new session/provider is not reconciliation of an already dispatched operation.
// Reload/process restart loses this memory: it is not durable server idempotency.
// No client unlock for UNKNOWN; authorized server reconciliation is still required.
type Reservation = Readonly<{ token: symbol; phase: "pending" | "unknown" }>;
const operations = new Map<string, Map<number, Reservation>>();
const listeners = new Set<() => void>();
function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
function operation(principal: string, target: number) {
  return operations.get(principal)?.get(target);
}
function notify() {
  for (const listener of listeners) listener();
}
function reserve(principal: string, target: number) {
  if (operation(principal, target)) return;
  const reservation: Reservation = {
    token: Symbol("deactivate-user"),
    phase: "pending",
  };
  const targets = operations.get(principal) ?? new Map<number, Reservation>();
  targets.set(target, reservation);
  operations.set(principal, targets);
  notify();
  return reservation;
}
function settle(
  principal: string,
  target: number,
  reservation: Reservation,
  result: DeactivationResult,
) {
  if (operation(principal, target) !== reservation) return;
  const targets = operations.get(principal)!;
  if (result.outcome === "OUTCOME_UNKNOWN")
    targets.set(target, { ...reservation, phase: "unknown" });
  else {
    // Only the original attempt's definitive response or non-dispatch can release it.
    targets.delete(target);
    if (!targets.size) operations.delete(principal);
  }
  notify();
}
/** A current dependency preview precedes confirmation; the server independently rechecks every mutation. */
export function UserDeactivationCandidate({
  userId,
  dispatchEnabled = false,
}: {
  userId: number;
  dispatchEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [
      state.session,
      state.status,
      state.pending,
      userId,
      dispatchEnabled,
      actions,
    ],
  );
  const latest = useRef(identity);
  latest.current = identity;
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null
  )
    return (
      <p role="status">
        Confirm an unrestricted session before reviewing deactivation.
      </p>
    );
  return (
    <Bound
      key={key(identity)}
      principal={state.session.user.id}
      userId={userId}
      enabled={dispatchEnabled}
      current={() => latest.current === identity}
      recover={() => void actions.revalidate()}
    />
  );
}
const keys = new WeakMap<object, number>();
let serial = 0;
function key(o: object) {
  if (!keys.has(o)) keys.set(o, ++serial);
  return keys.get(o)!;
}
function Bound({
  principal,
  userId,
  enabled,
  current,
  recover,
}: {
  principal: string;
  userId: number;
  enabled: boolean;
  current: () => boolean;
  recover: () => void;
}) {
  const reservation = useSyncExternalStore(
    subscribe,
    () => operation(principal, userId),
    () => undefined,
  );
  const [data, setData] = useState<{
    name: string;
    accountStatus: string;
    etag: string;
    blocked: string | null;
  } | null>(null);
  const [reason, setReason] = useState(""),
    [review, setReview] = useState(false),
    [phase, setPhase] = useState("loading"),
    [status, setStatus] = useState<number>();
  const [api] = useState(() =>
    createUserDeactivationCandidateApi(
      globalThis.fetch.bind(globalThis),
      enabled,
    ),
  );
  const alive = useRef(true),
    busy = useRef(false),
    attempted = useRef(false),
    controller = useRef<AbortController | null>(null);
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
      controller.current?.abort();
    };
  }, []);
  useEffect(() => {
    // A fresh account read cannot establish the outcome of an earlier PATCH.
    if (operation(principal, userId) || phase !== "loading") return;
    const abort = new AbortController();
    let reading = true;
    void (async () => {
      let response: Response;
      let preview;
      try {
        response = await fetch(`/api/users/${userId}/deactivation-preview`, {
          credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        });
        preview = response.ok ? decodeUserDeactivationPreview(await response.json(), userId) : null;
      } catch {
        if (reading && alive.current && current()) setPhase("unavailable");
        return;
      }
      if (!reading || !alive.current || !current()) return;
      if (!preview) {
        setPhase("unavailable");
        setStatus(response.status);
        return;
      }
      const blocked = preview.accountStatus !== "ACTIVE" ? "Only an Active account can be deactivated."
        : preview.lastDurableAdministrator ? "The last durable Administrator cannot be deactivated."
        : preview.coverageIncomplete ? "Dependencies require review before this account can be deactivated."
        : preview.hasOpenResponsibilities ? "Resolve the account's outstanding responsibilities before deactivation." : null;
      setData({
        name: `User ${userId}`,
        accountStatus: preview.accountStatus,
        etag: `"${preview.version}"`,
        blocked,
      });
      attempted.current = false;
      setPhase(blocked ? "blocked" : "ready");
    })();
    return () => {
      reading = false;
      abort.abort();
    };
  }, [principal, userId, reservation, phase]);
  async function confirm() {
    if (
      !enabled ||
      busy.current ||
      attempted.current ||
      !data ||
      data.blocked !== null ||
      !review ||
      reason.trim().length < 10 ||
      !current()
    )
      return;
    const reserved = reserve(principal, userId);
    if (!reserved) return;
    busy.current = true;
    attempted.current = true;
    const abort = new AbortController();
    controller.current = abort;
    setPhase("pending");
    const result = await api.deactivate(
      userId,
      data.etag,
      reason,
      () => alive.current && current(),
      abort.signal,
    );
    // Settle the original operation even when its editor has been replaced.
    // This never writes into the new session's editor or triggers session recovery.
    settle(principal, userId, reserved, result);
    if (!alive.current || !current()) return;
    setData(null);
    setReason("");
    setReview(false);
    busy.current = false;
    if (result.outcome === "CONFIRMED") setPhase("confirmed");
    else {
      setPhase(result.outcome === "OUTCOME_UNKNOWN" ? "unknown" : "rejected");
      setStatus(result.status);
    }
  }
  return (
    <main>
      <h1>Deactivate user account</h1>
      {data && !reservation && (
        <section aria-label="Target account">
          <p>
            {data.name}; Account ID {userId}; {data.accountStatus}
          </p>
        </section>
      )}
      {phase === "ready" && !reservation && (
        <>
          <label>
            Reason
            <textarea
              value={reason}
              disabled={review}
              onChange={(e) => setReason(e.target.value)}
            />
          </label>
          {!review ? (
            <button
              disabled={reason.trim().length < 10}
              onClick={() => setReview(true)}
            >
              Review deactivation
            </button>
          ) : (
            <>
              <p>
                Confirm this account and reason. The server must authorize and
                check all responsibilities.
              </p>
              <button disabled={!enabled} onClick={() => void confirm()}>
                Confirm deactivate
              </button>
              <button onClick={() => setReview(false)}>Edit reason</button>
            </>
          )}
        </>
      )}
      {phase === "blocked" && !reservation && <>
        <p role="alert">{data?.blocked}</p>
        <UserDeactivationReview userId={userId} autoLoad />
        <button type="button" onClick={() => { setData(null); setReview(false); setPhase("loading"); }}>Review account again</button>
      </>}
      {phase === "loading" && !reservation && (
        <p role="status">Loading account...</p>
      )}
      {(reservation?.phase === "pending" ||
        (phase === "pending" && !reservation)) && (
        <p role="status">Deactivation pending...</p>
      )}
      {phase === "confirmed" && (
        <p role="status">Account deactivated. Server confirmation received.</p>
      )}
      {(reservation?.phase === "unknown" ||
        ["unavailable", "rejected", "unknown"].includes(phase)) && (
        <p role="alert">
          {reservation?.phase === "unknown" || phase === "unknown"
            ? "Outcome unknown. Do not replay; ask an authorized owner to verify."
            : status === 412
              ? "Version changed. Obtain a new read and review; no replay."
              : status === 403
                ? "Access denied by the server."
                : status === 409
                  ? "Deactivation blocked by account state or responsibilities."
                  : "Deactivation unavailable. No automatic retry."}
        </p>
      )}
      {status === 401 && <button onClick={recover}>Check session</button>}
    </main>
  );
}
