import {
  createDeactivationPreviewApi,
  type DependencyPreview,
} from "./deactivationPreviewApi";
import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createStructureDeactivationApi } from "./structureDeactivationApi";
import {
  failureMessage,
  safeId,
  validReason,
  type Snapshot,
  type Failure,
  type StructureKind,
} from "./contracts";
const keys = new WeakMap<object, number>();
let next = 0;
function keyFor(x: object) {
  if (!keys.has(x)) keys.set(x, ++next);
  return keys.get(x)!;
}
/** Standalone preparation only; production router does not mount or enable this panel. */
export function StructureDeactivationPanel({
  kind,
  branchId,
  id,
  dispatchEnabled = false,
}: {
  kind: StructureKind;
  branchId: number;
  id: number;
  dispatchEnabled?: boolean;
}) {
  const { state } = useAuth();
  const identity = useMemo(
    () => ({}),
    [
      state.session,
      state.status,
      state.pending,
      kind,
      branchId,
      id,
      dispatchEnabled,
    ],
  );
  const latest = useRef(identity);
  latest.current = identity;
  const current = useMemo(() => () => latest.current === identity, [identity]);
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.session.mustChangePassword ||
    state.pending !== null
  )
    return <p role="status">A confirmed unrestricted session is required.</p>;
  if (!safeId(branchId) || !safeId(id) || (kind !== "floor" && kind !== "area"))
    return <p role="alert">Choose valid branch and record IDs.</p>;
  return (
    <Bound
      key={keyFor(identity)}
      {...{ kind, branchId, id, dispatchEnabled, current }}
    />
  );
}
function Bound({
  kind,
  branchId,
  id,
  dispatchEnabled,
  current,
}: {
  kind: StructureKind;
  branchId: number;
  id: number;
  dispatchEnabled: boolean;
  current: () => boolean;
}) {
  const [previewApi] = useState(() => createDeactivationPreviewApi());
  const [api] = useState(() =>
    createStructureDeactivationApi(
      globalThis.fetch.bind(globalThis),
      dispatchEnabled,
    ),
  );
  const [data, setData] = useState<DependencyPreview | null>(null),
    [error, setError] = useState<Failure | null>(null),
    [loading, setLoading] = useState(true),
    [reload, setReload] = useState(0),
    [reason, setReason] = useState(""),
    [review, setReview] = useState<{ reason: string; etag: string } | null>(
      null,
    ),
    [message, setMessage] = useState(""),
    [pending, setPending] = useState(false),
    [locked, setLocked] = useState(false);
  const busy = useRef(false),
    attempted = useRef(false),
    mounted = useRef(false),
    writer = useRef<AbortController | null>(null);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      writer.current?.abort();
    };
  }, []);
  useEffect(() => {
    const abort = new AbortController();
    let active = true;
    setData(null);
    setError(null);
    setLoading(true);
    setReview(null);
    setReason("");
    void previewApi.read(kind, branchId, id, abort.signal).then((r) => {
      if (!active || !current()) return;
      setLoading(false);
      if (r.ok) setData(r.data);
      else setError(r.error);
    });
    return () => {
      active = false;
      abort.abort();
    };
  }, [previewApi, kind, branchId, id, reload, current]);
  async function confirm() {
    if (
      !review ||
      !data ||
      busy.current ||
      attempted.current ||
      !current() ||
      !mounted.current ||
      !dispatchEnabled
    )
      return;
    busy.current = true;
    attempted.current = true;
    setPending(true);
    writer.current = new AbortController();
    const result = await api.deactivate(
      kind,
      branchId,
      id,
      review.etag,
      { reason: review.reason, confirmation: true },
      () => mounted.current && current(),
      writer.current.signal,
    );
    if (!mounted.current || !current()) return;
    busy.current = false;
    setPending(false);
    setReview(null);
    setLocked(true);
    if (result.outcome === "CONFIRMED") {
      setData({
        ...result.data,
        dependencies: [],
        writerCoordinationRequired: true,
      });
      setMessage("Deactivation confirmed by server.");
    } else {
      setError(result.error);
      setMessage(
        result.outcome === "OUTCOME_UNKNOWN"
          ? "Outcome unknown. Do not replay; ask an authorized owner to verify the record."
          : "Not confirmed. Start a new flow only after reviewing the record.",
      );
    }
  }
  return (
    <main>
      <h1>Deactivate {kind}</h1>
      <p>
        Branch {branchId}. Backend decides access; selecting an ID grants no
        authority.
      </p>
      {!dispatchEnabled && (
        <p role="status">
          Live deactivation disabled pending backend acceptance and Project
          Leader approval.
        </p>
      )}
      {loading && <p role="status">Loading record...</p>}
      {error && (
        <section role="alert">
          <p>{failureMessage(error)}</p>
          {error.dependencies?.map((d) => (
            <p key={d.kind}>
              {d.kind}: {d.count}
            </p>
          ))}
        </section>
      )}
      {data && (
        <>
          <dl>
            <dt>Code</dt>
            <dd>{data.record.code}</dd>
            <dt>Name</dt>
            <dd>{data.record.name}</dd>
            <dt>Status</dt>
            <dd>{data.record.isActive ? "Active" : "Inactive"}</dd>
            <dt>Description</dt>
            <dd>{data.record.description ?? "None"}</dd>
          </dl>
          <p>
            Dependency preview is a current snapshot, not permission or a
            reservation. Writer coordination remains unaccepted.
          </p>
          <section aria-label="Dependency preview">
            {data.dependencies.length === 0 ? (
              <p>
                No blockers observed in this snapshot. Deactivation remains
                disabled.
              </p>
            ) : (
              data.dependencies.map((d) => (
                <p key={d.kind}>
                  {d.kind}: {d.count}
                </p>
              ))
            )}
          </section>
          {!locked && data.record.isActive && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                if (validReason(reason) && current()) {
                  setReview({ reason, etag: data.etag });
                  setMessage("");
                } else
                  setMessage(
                    "Enter a reason of 10–1000 characters without control characters.",
                  );
              }}
            >
              <label>
                Reason
                <textarea
                  value={reason}
                  onChange={(e) => {
                    setReason(e.target.value);
                    setReview(null);
                  }}
                />
              </label>
              <button disabled={pending}>Review</button>
            </form>
          )}
          {review && !locked && (
            <section aria-label="Review deactivation">
              <h2>Review deactivation</h2>
              <p>
                {data.record.code} — {data.record.name}
              </p>
              <p>{review.reason}</p>
              <button disabled={pending} onClick={() => setReview(null)}>
                Cancel review
              </button>
              <button
                disabled={!dispatchEnabled || pending}
                onClick={() => void confirm()}
              >
                Confirm deactivation
              </button>
            </section>
          )}
        </>
      )}
      {message && <p role="status">{message}</p>}
      <button
        disabled={pending || locked}
        onClick={() => {
          setReload((n) => n + 1);
          setMessage("");
        }}
      >
        Refresh record
      </button>
    </main>
  );
}
