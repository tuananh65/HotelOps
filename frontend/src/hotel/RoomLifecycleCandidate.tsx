import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createHotelApi } from "./hotelApi";
import { strongTag, type RoomDto } from "./contracts";
import { createRoomLifecycleApi } from "./roomLifecycleApi";
/** Standalone candidate; do not mount until dependency/status/version/SQL release review. */
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
    [state.session, state.status, state.pending, branchId, roomId],
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
  const [data, setData] = useState<{ room: RoomDto; etag: string } | null>(
      null,
    ),
    [reason, setReason] = useState(""),
    [review, setReview] = useState(false),
    [phase, setPhase] = useState("loading"),
    [status, setStatus] = useState<number>();
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
    void createHotelApi()
      .read("roomEdit", { branchId, id: roomId }, {}, abort.signal)
      .then((r) => {
        if (!alive.current || !current()) return;
        if (r.ok && strongTag(r.etag)) {
          setData({ room: r.data, etag: r.etag });
          setPhase("ready");
        } else {
          setPhase("unavailable");
          if (!r.ok) setStatus(r.error.status);
        }
      });
    return () => {
      alive.current = false;
      abort.abort();
      controller.current?.abort();
    };
  }, [branchId, roomId]);
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
    }
  }
  const valid =
    reason.trim().length >= 10 &&
    reason.trim().length <= 1000 &&
    !/[\x00-\x1f\x7f-\x9f]/.test(reason);
  return (
    <main>
      <h1>Room lifecycle candidate</h1>
      <p>
        Release blocked. Cleaning, rework, inspection, incident, effective block
        and asset coverage is not yet certified. Empty data is not clearance.
      </p>
      {data && (
        <section aria-label="Selected room">
          <h2>Room {data.room.roomNumber}</h2>
          <p>
            Branch {data.room.branchId};{" "}
            {data.room.isActive ? "Active" : "Inactive"};{" "}
            {data.room.operationalStatus}
          </p>
          <p>
            {data.room.isActive
              ? "Deactivate to Inactive."
              : "Reactivate requires eligible active parents and Pending Cleaning until inspection; never infer Ready."}
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
            <button disabled={!valid} onClick={() => setReview(true)}>
              Review room change
            </button>
          ) : (
            <>
              <p>
                Confirm this exact room and reason. The server authorizes the
                transition and all dependencies.
              </p>
              <button disabled={!enabled} onClick={() => void confirm()}>
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
        <p role="status">Server confirmed room change.</p>
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
      {status === 401 && <button onClick={recover}>Check session</button>}
    </main>
  );
}
