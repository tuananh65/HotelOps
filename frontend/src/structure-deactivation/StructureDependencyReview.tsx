import { useEffect, useMemo, useRef, useState } from "react";
import { Link, Navigate, useLocation, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { decimalId } from "../hotel/contracts";
import {
  createDeactivationPreviewApi,
  type DependencyPreview,
} from "./deactivationPreviewApi";
import type { Failure, StructureKind } from "./contracts";
const identities = new WeakMap<object, number>();
let sequence = 0;
function identityKey(value: object) {
  if (!identities.has(value)) identities.set(value, ++sequence);
  return identities.get(value)!;
}
export function StructureDependencyReview({ kind }: { kind: StructureKind }) {
  const { state, actions } = useAuth();
  const { id: rawId } = useParams();
  const location = useLocation();
  const branchId = decimalId(
    new URLSearchParams(location.search).get("branchId") ?? "",
  );
  const id = decimalId(rawId ?? "");
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, kind, branchId, id],
  );
  const latest = useRef(identity);
  latest.current = identity;
  const current = useMemo(() => () => latest.current === identity, [identity]);
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate to="/login" replace />;
  if (state.status === "RESTRICTED" || state.session?.mustChangePassword)
    return <Navigate to="/change-password" replace />;
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null
  )
    return (
      <p role="status">Confirm your session before reviewing dependencies.</p>
    );
  if (!branchId || !id)
    return <p role="alert">Choose valid branch and location IDs.</p>;
  return (
    <Review
      key={identityKey(identity)}
      {...{ kind, branchId, id, current }}
      revalidate={() => void actions.revalidate()}
    />
  );
}
const labels: Record<string, string> = {
  ACTIVE_ROOM: "Active rooms",
  ACTIVE_CHILD_AREA: "Active child areas",
  ACTIVE_ASSET: "Active assets",
  OPEN_INCIDENT: "Open incidents",
  OPEN_OPERATIONAL_TASK: "Open tasks",
  TASK_COMPLETION_CONTRACT_UNRESOLVED: "Tasks requiring completion review",
  OPEN_MAINTENANCE_WORK: "Open maintenance work",
};
function Review({
  kind,
  branchId,
  id,
  current,
  revalidate,
}: {
  kind: StructureKind;
  branchId: number;
  id: number;
  current: () => boolean;
  revalidate: () => void;
}) {
  const [api] = useState(() => createDeactivationPreviewApi());
  const [data, setData] = useState<DependencyPreview | null>(null);
  const [error, setError] = useState<Failure | null>(null);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    const abort = new AbortController();
    let active = true;
    setData(null);
    setError(null);
    setLoading(true);
    void api.read(kind, branchId, id, abort.signal).then((result) => {
      if (!active || !current()) return;
      setLoading(false);
      if (result.ok) setData(result.data);
      else setError(result.error);
    });
    return () => {
      active = false;
      abort.abort();
    };
  }, [api, kind, branchId, id, current, reload]);
  return (
    <main>
      <h1>Review {kind} dependencies</h1>
      <Link to={`/${kind}s?branchId=${branchId}`}>Back to locations</Link>
      <p>
        This review does not deactivate the location. Deactivation remains
        unavailable pending backend acceptance.
      </p>
      {loading && <p role="status">Loading dependencies...</p>}
      {error && (
        <p role="alert">
          {error.status === 401
            ? "Session invalid. Check your session before continuing."
            : error.status === 403
              ? "You do not have access to review this location."
              : "Dependency review is unavailable. No action was performed."}
        </p>
      )}
      {data && (
        <section aria-label="Location dependencies">
          <h2>{data.record.name}</h2>
          <p>
            {data.record.code} / Branch {data.record.branchId} /{" "}
            {data.record.isActive ? "Active" : "Inactive"}
          </p>
          {data.dependencies.length ? (
            <ul>
              {data.dependencies.map((d) => (
                <li key={d.kind}>
                  {labels[d.kind]}: {d.count}
                </li>
              ))}
            </ul>
          ) : (
            <p>No dependencies were reported by this read.</p>
          )}
          <p>
            Dependencies may change after this read. An empty list does not
            approve deactivation.
          </p>
        </section>
      )}
      <button
        disabled={loading || error?.status === 401}
        onClick={() => setReload((value) => value + 1)}
      >
        Refresh dependencies
      </button>
      {error?.status === 401 && (
        <button onClick={revalidate}>Check session</button>
      )}
    </main>
  );
}
