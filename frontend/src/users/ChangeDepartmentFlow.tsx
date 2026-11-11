import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import {
  createChangeDepartmentApi,
  type DepartmentResult,
} from "./changeDepartmentApi";
// Not production-mounted. Review access never grants mutation authority.
const keys = new WeakMap<object, number>();
let sequence = 0;
function key(o: object) {
  if (!keys.has(o)) keys.set(o, ++sequence);
  return keys.get(o)!;
}
export function ChangeDepartmentFlow({
  userId,
  dispatchEnabled = false,
}: {
  userId: number;
  dispatchEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, userId, dispatchEnabled],
  );
  const latest = useRef(identity);
  latest.current = identity;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return <p role="status">Confirm an unrestricted session.</p>;
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
  const [api] = useState(() =>
    createChangeDepartmentApi(globalThis.fetch.bind(globalThis), enabled),
  );
  const [review, setReview] = useState<Extract<
    DepartmentResult,
    { ok: true }
  > | null>(null);
  const [destination, setDestination] = useState("");
  const [phase, setPhase] = useState("loading");
  const [status, setStatus] = useState<number>();
  const active = useRef(false),
    busy = useRef(false),
    attempted = useRef(false),
    abort = useRef<AbortController | null>(null);
  useEffect(() => {
    active.current = true;
    const controller = new AbortController();
    abort.current = controller;
    void api.read(userId, controller.signal).then((r) => {
      if (!active.current || !current() || controller.signal.aborted) return;
      if (r.ok) {
        setReview(r);
        setPhase("ready");
      } else {
        setStatus(r.status);
        setPhase("error");
      }
    });
    return () => {
      active.current = false;
      controller.abort();
    };
  }, [api, userId]);
  const valid =
    /^[1-9][0-9]*$/.test(destination) &&
    Number.isSafeInteger(Number(destination)) &&
    review &&
    Number(destination) !== review.data.departmentId;
  async function confirm() {
    if (
      !enabled ||
      !valid ||
      !review ||
      busy.current ||
      attempted.current ||
      !active.current ||
      !current()
    )
      return;
    busy.current = true;
    attempted.current = true;
    setPhase("pending");
    const controller = new AbortController();
    abort.current = controller;
    const r = await api.change(
      review.data,
      review.etag,
      Number(destination),
      () => active.current && current(),
      controller.signal,
    );
    if (!active.current || !current()) return;
    busy.current = false;
    setReview(null);
    setDestination("");
    if (r.ok) setPhase("confirmed");
    else {
      setStatus(r.status);
      setPhase(r.outcome === "OUTCOME_UNKNOWN" ? "unknown" : "error");
    }
  }
  return (
    <main>
      <h1>Change department</h1>
      <p>
        Same primary branch only. Existing role history blocks this restricted
        V1. Dispatch remains disabled until acceptance.
      </p>
      {review && (
        <section aria-label="Server scope">
          <p>
            User {review.data.userId}; Branch {review.data.branchId}; Department{" "}
            {review.data.departmentId}
          </p>
        </section>
      )}
      {phase === "loading" && <p role="status">Loading department review...</p>}
      {phase === "ready" && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (valid) setPhase("review");
          }}
        >
          <label>
            Destination Department ID
            <input
              aria-label="Destination Department ID"
              value={destination}
              onChange={(e) => setDestination(e.target.value)}
            />
          </label>
          <button disabled={!valid}>Review change</button>
        </form>
      )}
      {phase === "review" && (
        <section>
          <p>
            Confirm destination Department {destination}. The server verifies
            scope and all dependencies.
          </p>
          <button onClick={() => setPhase("ready")}>Back</button>
          <button disabled={!enabled} onClick={() => void confirm()}>
            Confirm department change
          </button>
        </section>
      )}
      {phase === "pending" && <p role="status">Department change pending...</p>}
      {phase === "confirmed" && (
        <p role="status">
          Department change confirmed. Target sessions revoked.
        </p>
      )}
      {["error", "unknown"].includes(phase) && (
        <p role="alert">
          {phase === "unknown"
            ? "Outcome unknown. Do not replay; ask an authorized owner to verify."
            : status === 401
              ? "Session invalid. Revalidate before a new review."
              : status === 403
                ? "Access denied."
                : status === 412
                  ? "Version changed. Start a new review; do not replay."
                  : status === 409
                    ? "Scope, role or open-work conflict. No change confirmed."
                    : "Department change unavailable. No change confirmed."}
        </p>
      )}
      {status === 401 && <button onClick={recover}>Check session</button>}
    </main>
  );
}
