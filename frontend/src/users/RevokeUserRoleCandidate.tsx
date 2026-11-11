import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createUserProfileApi } from "./userProfileApi";
import { createUsersApi } from "./usersApi";
import { createRevokeUserRoleApi } from "./revokeUserRoleApi";
/** Standalone candidate, intentionally not imported by production routes. Read access is not revoke authority. */
export function RevokeUserRoleCandidate({
  userId,
  assignmentId,
  dispatchEnabled = false,
}: {
  userId: number;
  assignmentId: number;
  dispatchEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, userId, assignmentId],
  );
  const latest = useRef(identity);
  latest.current = identity;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return (
      <p role="status">
        Confirm an unrestricted session before reviewing a role.
      </p>
    );
  return (
    <Bound
      key={key(identity)}
      userId={userId}
      assignmentId={assignmentId}
      dispatchEnabled={dispatchEnabled}
      current={() => latest.current === identity}
      recover={() => void actions.revalidate()}
    />
  );
}
const keys = new WeakMap<object, number>();
let sequence = 0;
function key(o: object) {
  if (!keys.has(o)) keys.set(o, ++sequence);
  return keys.get(o)!;
}
function Bound({
  userId,
  assignmentId,
  dispatchEnabled,
  current,
  recover,
}: {
  userId: number;
  assignmentId: number;
  dispatchEnabled: boolean;
  current: () => boolean;
  recover: () => void;
}) {
  const [data, setData] = useState<{
    etag: string;
    name: string;
    role: string;
    branch: number | null;
  } | null>(null);
  const [phase, setPhase] = useState("loading");
  const [review, setReview] = useState(false);
  const [status, setStatus] = useState<number>();
  const [api] = useState(() =>
    createRevokeUserRoleApi(globalThis.fetch.bind(globalThis), dispatchEnabled),
  );
  const active = useRef(true),
    busy = useRef(false),
    attempted = useRef(false),
    abort = useRef<AbortController | null>(null);
  useEffect(() => {
    active.current = true;
    const controller = new AbortController();
    abort.current = controller;
    void (async () => {
      const profile = await createUserProfileApi().getProfile(
        userId,
        controller.signal,
      );
      if (!active.current || !current()) return;
      if (!profile.ok) {
        setPhase("unavailable");
        setStatus(profile.error.status);
        return;
      }
      const access = await createUsersApi().getEffectiveAccess(
        userId,
        undefined,
        controller.signal,
      );
      if (!active.current || !current()) return;
      if (!access.ok) {
        setPhase("unavailable");
        setStatus(
          access.error.kind === "http" ? access.error.status : undefined,
        );
        return;
      }
      const selected = access.data.assignedRoles.find(
        (r) => r.assignmentId === assignmentId,
      );
      if (!selected) {
        setPhase("unavailable");
        return;
      }
      setData({
        etag: profile.etag,
        name: profile.data.fullName,
        role: selected.name,
        branch: selected.branchId,
      });
      setPhase("ready");
    })();
    return () => {
      active.current = false;
      controller.abort();
      abort.current?.abort();
    };
    // Bound is keyed by session and selectors; no old protected snapshot survives identity replacement.
  }, [userId, assignmentId]);
  async function confirm() {
    if (
      busy.current ||
      attempted.current ||
      !data ||
      !review ||
      !dispatchEnabled ||
      !current()
    )
      return;
    busy.current = true;
    attempted.current = true;
    setPhase("pending");
    const controller = new AbortController();
    abort.current = controller;
    const result = await api.revoke(
      userId,
      assignmentId,
      data.etag,
      () => active.current && current(),
      controller.signal,
    );
    if (!active.current || !current()) return;
    setData(null);
    setReview(false);
    busy.current = false;
    if (result.outcome === "CONFIRMED") setPhase("confirmed");
    else {
      setStatus(result.status);
      setPhase(
        result.reason === "OPEN_WORK"
          ? "blocked"
          : result.outcome === "OUTCOME_UNKNOWN"
            ? "unknown"
            : "rejected",
      );
    }
  }
  return (
    <main>
      <h1>Revoke user role candidate</h1>
      <p>
        Business activation is blocked pending dependency and SQL acceptance.
      </p>
      {data && (
        <section aria-label="Role to revoke">
          <p>
            {data.name}: {data.role}; Branch {data.branch ?? "unbound"}
          </p>
          <p>Assignment ID: {assignmentId}</p>
        </section>
      )}
      {phase === "ready" && !review && (
        <button onClick={() => setReview(true)}>Review revocation</button>
      )}
      {phase === "ready" && review && (
        <>
          <p>
            Review this exact assignment. The server decides whether revocation
            is permitted.
          </p>
          <button disabled={!dispatchEnabled} onClick={() => void confirm()}>
            Confirm revoke
          </button>
        </>
      )}
      {phase === "loading" && <p role="status">Loading server review...</p>}
      {phase === "pending" && <p role="status">Revocation pending...</p>}
      {phase === "confirmed" && (
        <p role="status">
          Role revoked. Target sessions must be invalidated by the server.
        </p>
      )}
      {["unavailable", "rejected", "unknown", "blocked"].includes(phase) && (
        <p role="alert">
          {phase === "blocked"
            ? "Open responsibilities prevent revocation. Arrange an authorized handover before a new review."
            : phase === "unknown"
              ? "Outcome unknown. Do not replay; ask an authorized owner to verify."
              : status === 412
                ? "Version changed. Reload and review in a new flow; no replay."
                : status === 403
                  ? "Access denied by the server."
                  : status === 503
                    ? "Revocation unavailable. Release gates remain closed."
                    : "Could not confirm revocation. No automatic retry."}
        </p>
      )}
      {status === 401 && <button onClick={recover}>Check session</button>}
    </main>
  );
}
