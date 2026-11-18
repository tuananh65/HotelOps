import { useEffect, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { strongTag, safeId } from "./contracts";

type View = { roomId: number; branchId: number; departmentIds: number[]; options: { id: number; name: string; isActive: boolean }[] };
function decode(v: unknown): View {
  const d = v as View;
  if (!d || !safeId(d.roomId) || !safeId(d.branchId) || !Array.isArray(d.departmentIds) || !d.departmentIds.every(safeId)
    || !Array.isArray(d.options) || !d.options.every(o => safeId(o.id) && typeof o.name === "string" && typeof o.isActive === "boolean")) throw Error("Phản hồi ánh xạ không hợp lệ");
  return d;
}
export function RoomDepartmentsPanel() {
  const { roomId } = useParams(); const [params] = useSearchParams();
  const room = Number(roomId), branch = Number(params.get("branchId"));
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const key = String(room) + ":" + String(branch);
  const live = useRef({ owner, key }); live.current = { owner, key };
  const [reload, setReload] = useState(0);
  const [data, setData] = useState<{ owner: typeof owner; key: string; view: View; tag: string } | null>(null);
  const [selected, setSelected] = useState<number[]>([]), [reason, setReason] = useState("");
  const [message, setMessage] = useState(""), [busy, setBusy] = useState(false);
  const inFlight = useRef(false);
  const endpoint = "/api/rooms/" + room + "/departments?branchId=" + branch;
  useEffect(() => {
    const abort = new AbortController(); setData(null); setSelected([]); setReason(""); setMessage("");
    if (!owner || !safeId(room) || !safeId(branch)) return;
    void fetch(endpoint, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal })
      .then(async r => { if (!r.ok) throw Error("Phân công chưa khả dụng (HTTP" + r.status + ").");
        const view = decode(await r.json()), tag = r.headers.get("ETag");
        if (view.roomId !== room || view.branchId !== branch || !strongTag(tag)) throw Error("Ngữ cảnh ánh xạ không hợp lệ");
        if (!abort.signal.aborted) { setData({ owner, key, view, tag }); setSelected(view.departmentIds); }
      }).catch(e => { if (!abort.signal.aborted) setMessage(String(e)); });
    return () => abort.abort();
  }, [owner, key, reload, endpoint, room, branch]);
  const visible = owner && data?.owner === owner && data.key === key ? data : null;
  async function save() {
    if (!visible || inFlight.current || reason.trim().length < 10) return;
    inFlight.current = true; setBusy(true); setMessage(""); let dispatched = false;
    const current = () => live.current.owner === owner && live.current.key === key;
    try {
      const csrf = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error" });
      if (!csrf.ok) throw Error("Không lấy được mã bảo vệ phiên CSRF.");
      const token = await csrf.json() as { headerName?: string; csrfToken?: string };
      if (token.headerName !== "X-CSRF-TOKEN" || typeof token.csrfToken !== "string" || !/^[\x21-\x7e]{1,8192}$/.test(token.csrfToken)) throw Error("Phản hồi CSRF không hợp lệ");
      if (!current()) return;
      dispatched = true;
      const r = await fetch(endpoint, { method: "PUT", credentials: "same-origin", cache: "no-store", redirect: "error",
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token.csrfToken, "If-Match": visible.tag },
        body: JSON.stringify({ departmentIds: selected, reason: reason.trim() }) });
      if (!current()) return;
      setData(null);
      if (!r.ok) { setMessage(r.status === 412 ? "Phân công đã đổi. Tải lại và xem phân công mới nhất." : "Cập nhật bị từ chối (HTTP" + r.status + "). Tải lại trước khi thử tiếp."); return; }
      const view = decode(await r.json()), tag = r.headers.get("ETag");
      if (!strongTag(tag) || view.roomId !== room || view.branchId !== branch) throw Error("Phản hồi không hợp lệ");
      setData({ owner, key, view, tag }); setSelected(view.departmentIds); setReason(""); setMessage("Đã cập nhật bộ phận phụ trách phòng.");
    } catch (e) { if (current()) { if (dispatched) setData(null); setMessage(dispatched ? "Chưa xác định kết quả. Tải lại và xác minh phân công trước khi thay đổi; không gửi lại." : String(e)); } }
    finally { inFlight.current = false; setBusy(false); }
  }
  return <main><h1>Phân công bộ phận phụ trách phòng</h1><Link to={"/rooms/status?branchId=" + branch + "&roomId=" + room}>Trạng thái phòng</Link>
    <p>Chỉ phân loại trên tổng quan; không cấp quyền hoặc phân công vệ sinh.</p>
    <button disabled={busy} onClick={() => setReload(n => n + 1)}>Tải lại phân công</button>
    {owner && message && <p role="status">{message}</p>}
    {visible && <form onSubmit={e => { e.preventDefault(); void save(); }}><fieldset disabled={busy}><legend>Bộ phận thuộc chi nhánh này</legend>
      {visible.view.options.map(d => <label key={d.id}><input type="checkbox" checked={selected.includes(d.id)} disabled={!d.isActive && !selected.includes(d.id)}
        onChange={e => setSelected(ids => e.target.checked ? [...ids, d.id] : ids.filter(id => id !== d.id))} />{d.name}{!d.isActive && " (inactive; remove mapping)"}</label>)}
      <label>Lý do<textarea required minLength={10} maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /></label>
      <button disabled={reason.trim().length < 10}>Lưu bộ phận phụ trách phòng</button></fieldset></form>}
    {!owner && <p>Xác minh phiên đăng nhập để xem phân công này.</p>}
  </main>;
}
