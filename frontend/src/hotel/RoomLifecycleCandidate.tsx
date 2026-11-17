import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { roomDto, safeId, strongTag, type RoomDto } from "./contracts";
import { createRoomLifecycleApi } from "./roomLifecycleApi";
type Dependency = { kind: string; count: number };
function dependencies(value: unknown): Dependency[] | null {
  if (!Array.isArray(value) || !value.every(d => d && typeof d.kind === "string" && Number.isSafeInteger(d.count) && d.count > 0)) return null;
  return value;
}
export function RoomLifecycleRoute() {
  const { id } = useParams(); const [params] = useSearchParams();
  const roomId = Number(id), branchId = Number(params.get("branchId"));
  return safeId(roomId) && safeId(branchId) ? <RoomLifecycleCandidate roomId={roomId} branchId={branchId} dispatchEnabled /> : <p role="alert">Invalid room context.</p>;
}
export function RoomLifecycleCandidate({
  branchId,
  roomId,
  dispatchEnabled = false,
}: {
  branchId: number;
  roomId: number;
  dispatchEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, state.authorityGeneration, branchId, roomId],
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
        Confirm an unrestricted session before reviewing the room.
      </p>
    );
  return (
    <Bound
      key={key(identity)}
      branchId={branchId}
      roomId={roomId}
      enabled={dispatchEnabled}
      current={() => latest.current === identity}
      recover={() => void actions.revalidate()}
    />
  );
}
const keys = new WeakMap<object, number>();
let serial = 0;
function key(v: object) {
  if (!keys.has(v)) keys.set(v, ++serial);
  return keys.get(v)!;
}
function Bound({
  branchId,
  roomId,
  enabled,
  current,
  recover,
}: {
  branchId: number;
  roomId: number;
  enabled: boolean;
  current: () => boolean;
  recover: () => void;
}) {
  const [data, setData] = useState<{ room: RoomDto; etag: string; readiness: string; dependencies: Dependency[] } | null>(
      null,
    ),
    [reason, setReason] = useState(""),
    [review, setReview] = useState(false),
    [phase, setPhase] = useState("loading"),
    [status, setStatus] = useState<number>();
  const [blocked, setBlocked] = useState<Dependency[]>([]);
  const [reload, setReload] = useState(0);
  const [api] = useState(() =>
    createRoomLifecycleApi(globalThis.fetch.bind(globalThis), enabled),
  );
  const alive = useRef(true),
    busy = useRef(false),
    attempted = useRef(false),
    controller = useRef<AbortController | null>(null);
  useEffect(() => {
    alive.current = true;
    const abort = new AbortController();
    controller.current = abort;
    attempted.current = false; setData(null); setBlocked([]); setStatus(undefined); setPhase("loading");
    void fetch(`/api/rooms/${roomId}/lifecycle?branchId=${branchId}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal })
      .then(async r => {
        if (!r.ok) { if (alive.current && current() && !abort.signal.aborted) setStatus(r.status); throw Error("Unavailable"); }
        const v = await r.json(), room = roomDto(v.room), deps = dependencies(v.dependencies), etag = r.headers.get("ETag");
        if (!room || room.id !== roomId || room.branchId !== branchId || !deps || !strongTag(etag) || typeof v.effectiveReadiness !== "string") throw Error("Invalid response");
        if (!alive.current || !current() || abort.signal.aborted) return;
        setData({ room, etag, readiness: v.effectiveReadiness, dependencies: deps }); setPhase("ready");
      }).catch(() => { if (alive.current && current() && !abort.signal.aborted) setPhase("unavailable"); });
    return () => {
      alive.current = false;
      abort.abort();
      controller.current?.abort();
    };
  }, [branchId, roomId, reload]);
  async function confirm() {
    if (
      !enabled ||
      busy.current ||
      attempted.current ||
      !data ||
      !review ||
      !current()
    )
      return;
    busy.current = true;
    attempted.current = true;
    setPhase("pending");
    const abort = new AbortController();
    controller.current = abort;
    const r = await api.change(
      branchId,
      roomId,
      !data.room.isActive,
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
    if (r.outcome === "CONFIRMED") setPhase("confirmed");
    else {
      setPhase(r.outcome === "OUTCOME_UNKNOWN" ? "unknown" : "rejected");
      setStatus(r.status);
      setBlocked(r.dependencies ?? []);
    }
  }
  const valid =
    reason.trim().length >= 10 &&
    reason.trim().length <= 1000 &&
    !/[\x00-\x1f\x7f-\x9f]/.test(reason);
  return (
    <main>
      <h1>Room activation</h1>
      <Link to={`/structure/rooms/${roomId}?branchId=${branchId}`}>Back to room</Link>
      <p>
        Deactivation requires all open work and effective blocks to be resolved and all assets to be moved.
      </p>
      {data && (
        <section aria-label="Selected room">
          <h2>Room {data.room.roomNumber}</h2>
          <p>
            Branch {data.room.branchId};{" "}
            {data.room.isActive ? "Active" : "Inactive"};{" "}
            {data.readiness}
          </p>
          <p>
            {data.room.isActive
              ? "Deactivate to Inactive."
              : "Reactivate requires eligible active parents and Pending Cleaning until inspection; never infer Ready."}
          </p>
        </section>
      )}
      {(data?.dependencies.length || blocked.length) ? <section role="alert"><p>MSG34: Resolve these dependencies before deactivation.</p><ul>{(data?.dependencies ?? blocked).map(d => <li key={d.kind}>{d.kind}: {d.count}</li>)}</ul></section> : null}
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
            <button disabled={!valid || !!data?.dependencies.length} onClick={() => setReview(true)}>
              Review room change
            </button>
          ) : (
            <>
              <p>
                MSG33: Confirm this exact room and reason. The server authorizes the
                transition and all dependencies.
              </p>
              <button disabled={!enabled || !!data?.dependencies.length} onClick={() => void confirm()}>
                Confirm room change
              </button>
              <button onClick={() => setReview(false)}>Edit reason</button>
            </>
          )}
        </>
      )}
      {phase === "loading" && <p role="status">Loading room...</p>}
      {phase === "pending" && <p role="status">Room change pending...</p>}
      {phase === "confirmed" && (
        <p role="status">MSG08: Server confirmed room change. Refresh to view its current readiness.</p>
      )}
      {["unavailable", "rejected", "unknown"].includes(phase) && (
        <p role="alert">
          {phase === "unknown"
            ? "Outcome unknown. Do not replay; verify with an authorized owner."
            : status === 412
              ? "Room version changed. Obtain a fresh read and review in a new flow."
              : status === 403
                ? "Access denied by server."
                : status === 409
                  ? "Room dependencies, state or parents prevent this transition."
                  : "Room lifecycle unavailable. No automatic retry."}
        </p>
      )}
      {phase !== "pending" && phase !== "loading" && <button onClick={() => { setReview(false); setReason(""); setReload(x => x + 1); }}>Refresh room and dependencies</button>}
      {status === 401 && <button onClick={recover}>Check session</button>}
    </main>
  );
}
