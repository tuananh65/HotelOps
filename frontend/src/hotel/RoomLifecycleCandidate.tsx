import { displayCode } from "../i18n/vi";
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
  return safeId(roomId) && safeId(branchId) ? <RoomLifecycleCandidate roomId={roomId} branchId={branchId} dispatchEnabled /> : <p role="alert">Ngữ cảnh phòng không hợp lệ.</p>;
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
        Xác minh phiên không bị hạn chế trước khi xét duyệt phòng.
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
        if (!r.ok) { if (alive.current && current() && !abort.signal.aborted) setStatus(r.status); throw Error("Chưa khả dụng"); }
        const v = await r.json(), room = roomDto(v.room), deps = dependencies(v.dependencies), etag = r.headers.get("ETag");
        if (!room || room.id !== roomId || room.branchId !== branchId || !deps || !strongTag(etag) || typeof v.effectiveReadiness !== "string") throw Error("Phản hồi không hợp lệ");
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
      <h1>Kích hoạt phòng</h1>
      <Link to={`/structure/rooms/${roomId}?branchId=${branchId}`}>Quay lại phòng</Link>
      <p>
        Cần xử lý hết công việc đang mở, lệnh khóa hiệu lực và di chuyển mọi tài sản trước khi vô hiệu hóa.
      </p>
      {data && (
        <section aria-label="Phòng đã chọn">
          <h2>Phòng {data.room.roomNumber}</h2>
          <p>
            Chi nhánh {data.room.branchId};{" "}
            {data.room.isActive ? "Đang hoạt động" : "Ngừng hoạt động"};{" "}
            {displayCode(data.readiness)}
          </p>
          <p>
            {data.room.isActive
              ? "Chuyển sang ngừng hoạt động."
              : "Kích hoạt lại cần mục cha hoạt động hợp lệ và phòng chờ vệ sinh đến khi kiểm tra; không tự coi là sẵn sàng."}
          </p>
        </section>
      )}
      {(data?.dependencies.length || blocked.length) ? <section role="alert"><p>MSG34: Xử lý các ràng buộc này trước khi vô hiệu hóa.</p><ul>{(data?.dependencies ?? blocked).map(d => <li key={d.kind}>{displayCode(d.kind)}: {d.count}</li>)}</ul></section> : null}
      {phase === "ready" && (
        <>
          <label>
            Lý do
            <textarea
              value={reason}
              disabled={review}
              onChange={(e) => setReason(e.target.value)}
            />
          </label>
          {!review ? (
            <button disabled={!valid || !!data?.dependencies.length} onClick={() => setReview(true)}>
              Xem lại thay đổi phòng
            </button>
          ) : (
            <>
              <p>
                MSG33: Xác nhận đúng phòng và lý do. Máy chủ kiểm tra quyền chuyển trạng thái và mọi ràng buộc.
              </p>
              <button disabled={!enabled || !!data?.dependencies.length} onClick={() => void confirm()}>
                Xác nhận thay đổi phòng
              </button>
              <button onClick={() => setReview(false)}>Chỉnh sửa lý do</button>
            </>
          )}
        </>
      )}
      {phase === "loading" && <p role="status">Đang tải phòng...</p>}
      {phase === "pending" && <p role="status">Đang chờ thay đổi phòng...</p>}
      {phase === "confirmed" && (
        <p role="status">MSG08: Máy chủ đã xác nhận thay đổi phòng. Tải lại để xem mức sẵn sàng hiện tại.</p>
      )}
      {["unavailable", "rejected", "unknown"].includes(phase) && (
        <p role="alert">
          {phase === "unknown"
            ? "Chưa xác định kết quả. Không gửi lại; xác minh với người có trách nhiệm."
            : status === 412
              ? "Phiên bản phòng đã đổi. Tải lại và bắt đầu xét duyệt mới."
              : status === 403
                ? "Máy chủ từ chối quyền truy cập."
                : status === 409
                  ? "Ràng buộc, trạng thái hoặc mục cha của phòng không cho phép chuyển đổi này."
                  : "Chưa có chức năng vòng đời phòng. Không tự gửi lại."}
        </p>
      )}
      {phase !== "pending" && phase !== "loading" && <button onClick={() => { setReview(false); setReason(""); setReload(x => x + 1); }}>Tải lại phòng và ràng buộc</button>}
      {status === 401 && <button onClick={recover}>Kiểm tra phiên đăng nhập</button>}
    </main>
  );
}
