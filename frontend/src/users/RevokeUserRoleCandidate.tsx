import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createUserProfileApi } from "./userProfileApi";
import { createUsersApi } from "./usersApi";
import { createRevokeUserRoleApi } from "./revokeUserRoleApi";
import { Link } from "react-router-dom";
/** Read access is not revoke authority. The server rechecks the selected assignment. */
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
    [state.session, state.status, state.pending, userId, assignmentId],
  );
  const latest = useRef(identity);
  latest.current = identity;
  if (state.status !== "AUTHENTICATED" || state.pending !== null || !state.session)
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
  const [dependencies, setDependencies] = useState<string[]>([]);
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
      setDependencies(result.dependencies ?? []);
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
      <h1>Revoke user role</h1>
      <p>
        Revocation changes effective access and signs the account out of all sessions.
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
          Role revoked and target sessions invalidated.
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
                    ? "Revocation unavailable. Responsibility checks could not be completed."
                    : "Could not confirm revocation. No automatic retry."}
        </p>
      )}
      {phase === "blocked" && dependencies.length > 0 && <section aria-label="Work preventing revocation">
        <h2>Open responsibilities</h2>
        <ul>{dependencies.map(d => <li key={d}>{d.replaceAll("_", " ")}</li>)}</ul>
        {dependencies.length === 200 && <p>First 200 dependencies shown. Resolve them before reviewing again.</p>}
        <p>These identifiers do not grant access to the underlying records.</p>
      </section>}
      <Link to={`/users/${userId}`}>Return to account and review current access</Link>
      {status === 401 && <button onClick={recover}>Check session</button>}
    </main>
  );
}
