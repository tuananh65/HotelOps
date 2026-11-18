import { displayCode } from "../i18n/vi";
﻿import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
type Room = { roomId: number; number: string; readiness: string; inScope: boolean; blockedByIncident: boolean };
type Context = { incidentId: number; version: string; page: number; hasMore: boolean; rooms: Room[] };
const positive = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
export function IncidentRoomBlocksPanel({ incidentId, onCommitted }: { incidentId: number; onCommitted?: (notice: string) => void }) {
  const { state } = useAuth(), owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const active = useRef<AbortController | null>(null), sending = useRef(false);
  const [result, setResult] = useState<{ owner: typeof owner; data: Context } | null>(null);
  const [selected, setSelected] = useState<number[]>([]), [action, setAction] = useState("BLOCK"), [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false), [review, setReview] = useState(false), [attempted, setAttempted] = useState(false);
  const [message, setMessage] = useState<{ owner: typeof owner; text: string } | null>(null);
  useEffect(() => () => active.current?.abort(), [owner, incidentId]);
  const data = result?.owner === owner && result.data.incidentId === incidentId ? result.data : null;
  async function load(page: number) {
    if (!owner || sending.current) return;
    sending.current = true; setBusy(true); setResult(null); setSelected([]); setReview(false); const abort = new AbortController(); active.current = abort;
    try {
      const response = await fetch(`/api/incidents/${incidentId}/room-blocks?page=${page}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!response.ok) throw Error("Chưa có phạm vi phòng. Kiểm tra quyền và trạng thái sự cố hiện tại.");
      const r: unknown = await response.json();
      if (!isObject(r) || r.incidentId !== incidentId || r.page !== page || typeof r.hasMore !== "boolean" || typeof r.version !== "string" || !/^"[0-9A-F]{16}"$/.test(r.version)
        || !Array.isArray(r.rooms) || !r.rooms.every(v => isObject(v) && positive(v.roomId) && typeof v.number === "string" && typeof v.readiness === "string" && typeof v.inScope === "boolean" && typeof v.blockedByIncident === "boolean")) throw Error("Phản hồi phạm vi phòng không hợp lệ.");
      if (!abort.signal.aborted && current.current === owner) { setResult({ owner, data: r as Context }); setSelected([]); setReview(false); setAttempted(false); setMessage(null); }
    } catch (e) { if (!abort.signal.aborted && current.current === owner) setMessage({ owner, text: e instanceof Error ? e.message : "Phạm vi phòng chưa khả dụng." }); }
    finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); } }
  }
  const eligible = (r: Room) => action === "MAP" ? !r.inScope : r.inScope && (action === "BLOCK" ? !r.blockedByIncident : r.blockedByIncident);
  const valid = !!data && selected.length > 0 && selected.every(id => data.rooms.some(r => r.roomId === id && eligible(r))) && reason.trim().length >= 10;
  async function save() {
    if (!owner || !data || !valid || !review || sending.current || attempted) return;
    sending.current = true; setBusy(true); setAttempted(true); const abort = new AbortController(); active.current = abort;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!bootstrap.ok) throw Error("Không xác minh được phiên đăng nhập. Vui lòng tải lại.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !/^[\x21-\x7e]+$/.test(csrf.csrfToken) || current.current !== owner || abort.signal.aborted) throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      const response = await fetch(`/api/incidents/${incidentId}/room-blocks`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": data.version }, body: JSON.stringify({ action, roomIds: selected, reason: reason.trim() }) });
      if (response.status === 400) {
        const problem: unknown = await response.json();
        if (isObject(problem) && problem.title === "Invalid room block change" && !abort.signal.aborted && current.current === owner) {
          setAttempted(false);
          setMessage({ owner, text: typeof problem.detail === "string" ? problem.detail : "MSG03: Kiểm tra phòng đã chọn, lệnh khóa hiện có và lý do." });
          return;
        }
      }
      if (!response.ok) throw Error(response.status === 412 ? "Sự cố đã thay đổi. Tải lại danh sách phòng trước khi thử lại." : "Chưa xác nhận thay đổi phòng. Tải lại phòng, kiểm tra phạm vi, lý do và lệnh khóa hiện có.");
      const saved: unknown = await response.json();
      if (!isObject(saved) || saved.incidentId !== incidentId || saved.action !== action || typeof saved.version !== "string" || !/^"[0-9A-F]{16}"$/.test(saved.version)) throw Error("Chưa xác minh được phản hồi. Tải lại phòng.");
      if (!abort.signal.aborted && current.current === owner) {
        const notice = action === "MAP" ? "MSG08: Đã lưu phạm vi phòng bị ảnh hưởng. Tải lại phòng để khóa." : "MSG08: Đã cập nhật lệnh khóa phòng và tính lại mức sẵn sàng. Thông báo đang chờ gửi đến giám sát đủ điều kiện.";
        setResult(null); setSelected([]); setMessage({ owner, text: notice }); onCommitted?.(notice);
      }
    } catch (e) { if (!abort.signal.aborted && current.current === owner) setMessage({ owner, text: e instanceof Error ? e.message : "Chưa xác nhận thay đổi. Tải lại danh sách phòng." }); }
    finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); setReview(false); } }
  }
  if (!owner || !positive(incidentId)) return null;
  return <section aria-label="Lệnh khóa phòng do sự cố"><h3>Phòng bị ảnh hưởng và lệnh khóa</h3>
    <button disabled={busy || review} onClick={() => void load(data?.page ?? 1)}>Tải lại phòng</button>
    {message?.owner === owner && <p role="status">{message.text}</p>}
    {data && <><fieldset disabled={busy || attempted || review}><label>Thao tác<select value={action} onChange={e => { setAction(e.target.value); setSelected([]); }}>
      <option value="BLOCK">Khóa các phòng bị ảnh hưởng</option><option value="REMOVE">Xóa lệnh khóa do sự cố</option><option value="MAP">Thêm phòng vào phạm vi bị ảnh hưởng</option></select></label>
      <p>Mã phòng và phân công phòng bị ảnh hưởng xác định phạm vi. Bỏ khóa của sự cố này không bỏ khóa khác hoặc bỏ qua yêu cầu vệ sinh.</p>
      {data.rooms.map(r => <label key={r.roomId}><input type="checkbox" disabled={!eligible(r)} checked={selected.includes(r.roomId)} onChange={e => setSelected(e.target.checked ? [...selected, r.roomId] : selected.filter(id => id !== r.roomId))} />
        Phòng {r.number}: {displayCode(r.readiness)}; {r.inScope ? "trong phạm vi bị ảnh hưởng" : "ngoài phạm vi bị ảnh hưởng"}; {r.blockedByIncident ? "bị khóa bởi sự cố này" : "không có lệnh khóa từ sự cố này"}</label>)}
      <label>Lý do (ít nhất 10 ký tự)<textarea value={reason} onChange={e => setReason(e.target.value)} /></label></fieldset>
      <button disabled={busy || review || attempted || data.page <= 1} onClick={() => void load(data.page - 1)}>Phòng trước</button><button disabled={busy || review || attempted || !data.hasMore} onClick={() => void load(data.page + 1)}>Phòng tiếp theo</button>
      {review ? <><p>{action === "REMOVE" ? "MSG33: " : ""}Xác nhận {action.toLowerCase()} cho {selected.length} phòng đã chọn?</p><button disabled={busy} onClick={() => void save()}>Xác nhận thay đổi phòng</button><button disabled={busy} onClick={() => setReview(false)}>Hủy</button></>
        : <button disabled={!valid || busy || attempted} onClick={() => setReview(true)}>Xem lại thay đổi phòng</button>}</>}
  </section>;
}
