import { useEffect, useRef, useState } from "react";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError, getJson, send } from "../../api/dev1/client";
import { useWorkspacePermissions } from "../housekeeping/useWorkspacePermissions";

type Departure = { canDepart: boolean; version: string; recordedStatus: string; unavailableReason: string | null };
type Target = { roomId: number; roomNumber: string; floorName: string; roomTypeName: string; version: string };
type Move = { source: Departure; targets: Target[] };
const version = (v: unknown) => typeof v === "string" && /^[A-F0-9]{64}$/.test(v);
function departure(value: unknown): Departure {
  const d = value as Departure;
  if (!d || typeof d.canDepart !== "boolean" || !version(d.version) || typeof d.recordedStatus !== "string"
    || !(d.unavailableReason === null || typeof d.unavailableReason === "string")) throw new Error("Invalid departure response");
  return d;
}
function move(value: unknown, source: number): Move {
  const m = value as Move;
  departure(m?.source);
  if (!Array.isArray(m.targets) || m.targets.some(t => !Number.isSafeInteger(t.roomId) || t.roomId <= 0 || t.roomId === source
    || !version(t.version) || [t.roomNumber, t.floorName, t.roomTypeName].some(s => typeof s !== "string")))
    throw new Error("Invalid room targets");
  return m;
}

export function RoomDepartureActions({ branchId, roomId, roomNumber, occupied, onChanged }: {
  branchId: number; roomId: number; roomNumber: string; occupied: boolean; onChanged: () => void;
}) {
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const access = useWorkspacePermissions(branchId);
  const canOut = occupied && access.allows("ROOM_CHECKOUT");
  const canMove = occupied && access.allows("ROOM_OCCUPANCY_MOVE");
  const path = `/branches/${branchId}/rooms/${roomId}`;
  const [data, setData] = useState<{ owner: typeof owner; path: string; out: Departure | null; move: Move | null } | null>(null);
  const [mode, setMode] = useState<"out" | "move" | null>(null);
  const [target, setTarget] = useState("");
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [retry, setRetry] = useState(0);
  const flight = useRef(false);
  const identity = useRef({ owner, path });
  identity.current = { owner, path };
  useEffect(() => {
    setData(null); setMode(null); setTarget(""); setReason(""); setError("");
    if (!owner || (!canOut && !canMove)) return;
    const controller = new AbortController();
    void Promise.all([
      canOut ? getJson(`${path}/check-out`, controller.signal).then(departure) : null,
      canMove ? getJson(`${path}/move-options`, controller.signal).then(v => move(v, roomId)) : null,
    ]).then(([out, moves]) => {
      if (!controller.signal.aborted) setData({ owner, path, out, move: moves });
    }).catch(() => {
      if (!controller.signal.aborted) setError("Chưa tải được thao tác trả và chuyển phòng. Vui lòng tải lại.");
    });
    return () => controller.abort();
  }, [owner, path, roomId, canOut, canMove, retry]);
  const visible = owner && data?.owner === owner && data.path === path ? data : null;
  if (!owner || (!canOut && !canMove)) return null;
  async function submit() {
    if (flight.current || !visible || !mode) return;
    const source = mode === "out" ? visible.out : visible.move?.source;
    const destination = visible.move?.targets.find(t => String(t.roomId) === target);
    if (!source?.canDepart || (mode === "move" && (!destination || !reason.trim()))) return;
    flight.current = true; setBusy(true); setError("");
    const valid = () => identity.current.owner === owner && identity.current.path === path;
    try {
      const csrf = await getJson("/auth/csrf") as { headerName: string; csrfToken: string };
      if (!valid()) return;
      if (csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !csrf.csrfToken || /[\r\n]/.test(csrf.csrfToken))
        throw new Error("Invalid CSRF");
      const response = await send(`${path}/${mode === "out" ? "check-out" : "move"}`, undefined, {
        method: "POST", headers: { "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": `"${source.version}"`, "Content-Type": "application/json" },
        ...(mode === "move" ? { body: JSON.stringify({ targetRoomId: destination!.roomId, targetVersion: destination!.version, reason: reason.trim() }) } : {}),
      });
      if (!response.body || response.body.roomId !== roomId || response.body.recordedStatus !== "DIRTY"
        || (mode === "move" && response.body.targetRoomId !== destination!.roomId)) throw new Error("Invalid receipt");
      if (valid()) { setData(null); setMode(null); onChanged(); }
    } catch (e) {
      if (valid()) {
        setData(null); setMode(null);
        setError(e instanceof ApiError && [409, 412].includes(e.status)
          ? "Thông tin phòng đã thay đổi hoặc phòng không còn đủ điều kiện. Hãy tải lại trước khi thực hiện."
          : e instanceof ApiError && e.status === 403 ? "Bạn không còn quyền thực hiện thao tác này."
          : "Chưa xác nhận được kết quả. Hãy tải lại phòng để kiểm tra trước khi thử tiếp.");
      }
    } finally { flight.current = false; setBusy(false); }
  }
  return <section aria-label="Trả và chuyển phòng" className="ops-note">
    {!mode && <div className="form-actions">
      {visible?.out?.canDepart && <button className="ds-button" type="button" onClick={() => setMode("out")}>Trả phòng</button>}
      {visible?.move?.source.canDepart && <button className="ds-button" type="button" onClick={() => setMode("move")}>Chuyển phòng</button>}
    </div>}
    {visible && !(visible.out ?? visible.move?.source)?.canDepart && <p role="status">{(visible.out ?? visible.move?.source)?.unavailableReason}</p>}
    {mode && <form onSubmit={e => { e.preventDefault(); void submit(); }}>
      <h3>{mode === "out" ? `Trả phòng ${roomNumber}` : `Chuyển khách từ phòng ${roomNumber}`}</h3>
      <p>Phòng {roomNumber} sẽ chuyển sang chờ vệ sinh.</p>
      {mode === "move" && <>
        <label>Phòng đích
          <select aria-label="Phòng đích" required value={target} disabled={busy} onChange={e => setTarget(e.target.value)}>
            <option value="">Chọn phòng sẵn sàng</option>
            {visible?.move?.targets.map(t => <option key={t.roomId} value={t.roomId}>Phòng {t.roomNumber} · {t.floorName} · {t.roomTypeName}</option>)}
          </select>
        </label>
        {visible?.move?.targets.length === 0 && <p role="status">Chưa có phòng đủ điều kiện để chuyển khách.</p>}
        <label>Lý do chuyển phòng
          <input required maxLength={500} value={reason} disabled={busy} onChange={e => setReason(e.target.value)} />
        </label>
      </>}
      <div className="form-actions">
        <button className="ds-button ds-button--primary" type="submit" disabled={busy || (mode === "move" && (!target || !reason.trim()))}>
          {busy ? "Đang ghi nhận…" : mode === "out" ? "Xác nhận trả phòng" : "Xác nhận chuyển phòng"}
        </button>
        <button className="ds-button" type="button" disabled={busy} onClick={() => { setMode(null); setTarget(""); setReason(""); }}>Hủy</button>
      </div>
    </form>}
    {error && <div role="alert"><p>{error}</p><button className="ds-button" type="button" disabled={busy} onClick={() => { setRetry(n => n + 1); onChanged(); }}>Tải lại phòng</button></div>}
  </section>;
}
