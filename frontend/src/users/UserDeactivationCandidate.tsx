import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createUsersApi } from "./usersApi";
import { createUserProfileApi } from "./userProfileApi";
import { createUserDeactivationCandidateApi } from "./userDeactivationCandidateApi";
/** Intentionally absent from production routes; read identity never authorizes deactivation. */
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
    [state.session, state.status, state.pending, userId],
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
  userId,
  enabled,
  current,
  recover,
}: {
  userId: number;
  enabled: boolean;
  current: () => boolean;
  recover: () => void;
}) {
  const [data, setData] = useState<{
    name: string;
    accountStatus: string;
    etag: string;
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
    const abort = new AbortController();
    controller.current = abort;
    void (async () => {
      const profile = await createUserProfileApi().getProfile(
        userId,
        abort.signal,
      );
      if (!alive.current || !current()) return;
      if (!profile.ok) {
        setPhase("unavailable");
        setStatus(profile.error.status);
        return;
      }
      const account = await createUsersApi().getUser(userId, abort.signal);
      if (!alive.current || !current()) return;
      if (!account.ok) {
        setPhase("unavailable");
        setStatus(
          account.error.kind === "http" ? account.error.status : undefined,
        );
        return;
      }
      setData({
        name: account.data.fullName,
        accountStatus: account.data.accountStatus,
        etag: profile.etag,
      });
      setPhase("ready");
    })();
    return () => {
      alive.current = false;
      abort.abort();
      controller.current?.abort();
    };
  }, [userId]);
  async function confirm() {
    if (
      !enabled ||
      busy.current ||
      attempted.current ||
      !data ||
      !review ||
      reason.trim().length < 10 ||
      !current()
    )
      return;
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
      <h1>Deactivate user candidate</h1>
      <p>Business activation is blocked. No production route is enabled.</p>
      {data && (
        <section aria-label="Target account">
          <p>
            {data.name}; Account ID {userId}; {data.accountStatus}
          </p>
        </section>
      )}
      {phase === "ready" && (
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
      {phase === "loading" && <p role="status">Loading account...</p>}
      {phase === "pending" && <p role="status">Deactivation pending...</p>}
      {phase === "confirmed" && (
        <p role="status">Account deactivated. Server confirmation received.</p>
      )}
      {["unavailable", "rejected", "unknown"].includes(phase) && (
        <p role="alert">
          {phase === "unknown"
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
