import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Target = { departmentId: number; departmentName: string; userId: number; name: string };
type History = { id: number; number: string; state: string; createdAt: string; conditionNote: string | null; snapshot: string | null; decisionSnapshot: string | null; canResubmit: boolean };
type View = { assetId: number; assetCode: string; assetName: string; currentDepartmentId: number | null; custodianUserId: number | null;
  conditionStatus: string; lifecycleStatus: string; version: string; canSubmit: boolean; canDecide: boolean;
  targets: Target[]; pending: { handoverId: number; handoverNo: string; conditionNote: string | null }[];
  history: History[]; historyIncomplete: boolean; page: number; pageSize: number; total: number; photoLimits: { maxFiles: number; maxMegabytes: number; formats: string[] } };
function photo(file: File): Promise<{ name: string; mimeType: string; base64: string }> {
  return new Promise((resolve, reject) => { const reader = new FileReader(); reader.onerror = () => reject(Error("Không đọc được ảnh."));
    reader.onload = () => resolve({ name: file.name, mimeType: file.type, base64: String(reader.result).split(",")[1] }); reader.readAsDataURL(file); });
}
function evidence(row: History): { notes?: string; photo_ids?: number[]; previous_handover_id?: number } {
  try { return row.snapshot ? JSON.parse(row.snapshot) : {}; } catch { return {}; }
}
function decision(row: History): { reason?: string; decided_at?: string; to_user_id?: number } {
  try { return row.decisionSnapshot ? JSON.parse(row.decisionSnapshot) : {}; } catch { return {}; }
}
export function AssetCustodyPage() {
  const { id } = useParams(); const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const key = `${owner}:${id}`; const live = useRef(key); live.current = key;
  const [view, setView] = useState<{ key: string; value: View } | null>(null), [page, setPage] = useState(1), [reload, setReload] = useState(0);
  const [target, setTarget] = useState(""), [condition, setCondition] = useState(""), [notes, setNotes] = useState(""), [reason, setReason] = useState("");
  const [previous, setPrevious] = useState<number | null>(null), [files, setFiles] = useState<File[]>([]);
  const [message, setMessage] = useState(""), [busy, setBusy] = useState(false), [stale, setStale] = useState(false);
  const inFlight = useRef(false), writer = useRef<AbortController | null>(null);
  const data = owner && view?.key === key ? view.value : null;
  useEffect(() => {
    setView(null); setMessage(""); setStale(false); setPrevious(null); setFiles([]); setTarget(""); setCondition(""); setNotes(""); setReason("");
    writer.current?.abort(); inFlight.current = false; setBusy(false);
    if (!owner || !id || !/^[1-9]\d*$/.test(id) || !Number.isSafeInteger(Number(id))) return;
    const abort = new AbortController();
    void fetch(`/api/asset-custody/${id}?page=${page}`, { signal: abort.signal, credentials: "same-origin", cache: "no-store", redirect: "error" })
      .then(async r => { if (r.status === 401) actions.invalidateSession(owner); const value = await r.json();
        if (!r.ok) throw Error(value.detail ?? `Quyền truy cập chưa khả dụng (${r.status}).`);
        const v = value as View;
        if (v.assetId !== Number(id) || !/^"[0-9A-F]{16}"$/.test(v.version) || !Array.isArray(v.targets) || !Array.isArray(v.history) || !Array.isArray(v.pending)) throw Error("Phản hồi quản lý tài sản không hợp lệ.");
        if (!abort.signal.aborted && live.current === key) setView({ key, value: v });
      }).catch(e => { if (!abort.signal.aborted && live.current === key) setMessage(String(e)); });
    return () => { abort.abort(); writer.current?.abort(); };
  }, [id, owner, key, page, reload, actions]);
  async function mutate(action: "submit" | "acknowledge" | "reject", handover?: number) {
    if (!data || !owner || inFlight.current || stale) return;
    inFlight.current = true; setBusy(true); setMessage(""); const abort = new AbortController(); writer.current = abort; let dispatched = false;
    try {
      let body: unknown = {};
      if (action === "submit") {
        const selected = data.targets.find(t => `${t.departmentId}:${t.userId}` === target);
        if (!selected || !condition.trim() || files.length > data.photoLimits.maxFiles) throw Error("Chọn người nhận hợp lệ, ghi tình trạng và kiểm tra số ảnh.");
        for (const file of files) if (file.size > data.photoLimits.maxMegabytes * 1024 * 1024 || !["image/jpeg", "image/png"].includes(file.type)) throw Error("Ảnh vượt dung lượng hoặc không đúng định dạng cho phép.");
        body = { departmentId: selected.departmentId, receiverId: selected.userId, conditionNote: condition.trim(), notes: notes.trim() || null,
          previousHandoverId: previous, photos: await Promise.all(files.map(photo)) };
      } else if (action === "reject") { if (reason.trim().length < 10) throw Error("Lý do từ chối cần ít nhất 10 ký tự."); body = { reason: reason.trim() }; }
      if (abort.signal.aborted || live.current !== key) return;
      const csrfResponse = await fetch("/api/auth/csrf", { signal: abort.signal, credentials: "same-origin", cache: "no-store", redirect: "error" });
      if (csrfResponse.status === 401) actions.invalidateSession(owner);
      const csrf = await csrfResponse.json(); if (!csrfResponse.ok || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !csrf.csrfToken) throw Error("Chưa có bảo vệ phiên (CSRF).");
      if (abort.signal.aborted || live.current !== key) return;
      dispatched = true;
      const r = await fetch(`/api/asset-custody/${id}${action === "submit" ? "" : `/${handover}/${action}`}`, {
        method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": data.version }, body: JSON.stringify(body) });
      const result = await r.json(); if (r.status === 401) actions.invalidateSession(owner);
      if (abort.signal.aborted || live.current !== key) return;
      setStale(true);
      if (!r.ok) { setView(null); throw Error(result.detail ?? `Yêu cầu thất bại (${r.status}); tải lại trước khi thử lại.`); }
      setView(null); setFiles([]); setMessage(`${result.message ?? "MSG08"}: ${result.state}. Tải lại để xem người quản lý tài sản hiện tại.`);
    } catch (e) { if (!abort.signal.aborted && live.current === key) { setMessage(`${String(e)}${dispatched ? "Không tự gửi lại; tải lại để xác nhận trạng thái đã lưu." : ""}`); if (dispatched) { setStale(true); setView(null); } } }
    finally { if (writer.current === abort) { writer.current = null; inFlight.current = false; setBusy(false); } }
  }
  if (!owner) return <p>Cần phiên đăng nhập hiện tại không bị hạn chế.</p>;
  return <section><h1>Bàn giao trách nhiệm quản lý tài sản</h1><Link to={`/assets/${id}`}>Hồ sơ tài sản</Link>
    {message && <p role="status">{message}</p>}<button disabled={busy} onClick={() => setReload(x => x + 1)}>Tải lại quản lý tài sản</button>
    {data && <><h2>{data.assetCode} — {data.assetName}</h2><p>Bộ phận: {data.currentDepartmentId ?? "N/A"}; người quản lý tài sản: {data.custodianUserId ?? "N/A"}; Tình trạng: {displayCode(data.conditionStatus)}; Vòng đời: {displayCode(data.lifecycleStatus)}</p>
      {data.canSubmit && <fieldset disabled={busy || stale}><legend>{previous ? `Gửi lại phiên bản bị từ chối #${previous}` : "Bàn giao mới"}</legend>
        <label>Bộ phận / trưởng bộ phận tiếp nhận <select value={target} onChange={e => setTarget(e.target.value)}><option value="">Chọn</option>{data.targets.map(t => <option key={`${t.departmentId}:${t.userId}`} value={`${t.departmentId}:${t.userId}`}>{displayCode(t.departmentName)} — {t.name}</option>)}</select></label>
        <label>Tình trạng khi bàn giao <textarea maxLength={4000} value={condition} onChange={e => setCondition(e.target.value)} /></label>
        <label>Ghi chú <textarea maxLength={4000} value={notes} onChange={e => setNotes(e.target.value)} /></label>
        <label>Ảnh ({data.photoLimits.maxFiles} tối đa; {data.photoLimits.maxMegabytes} MiB mỗi tệp) <input key={`${reload}:${previous}`} type="file" multiple accept="image/jpeg,image/png" onChange={e => setFiles(Array.from(e.target.files ?? []))} /></label>
        <p>Gửi không đổi người quản lý tài sản. Chỉ đổi sau khi trưởng bộ phận tiếp nhận xác nhận.</p>
        <button disabled={!target || !condition.trim()} onClick={() => void mutate("submit")}>Gửi bàn giao</button>
        {previous && <button onClick={() => { setPrevious(null); setFiles([]); }}>Hủy gửi lại</button>}
      </fieldset>}
      {data.pending.map(p => <section key={p.handoverId}><h3>{p.handoverNo} — Chờ xác nhận</h3><p>{p.conditionNote}</p>
        {data.canDecide && <fieldset disabled={busy || stale}><legend>Quyết định của trưởng bộ phận tiếp nhận</legend>
          <button onClick={() => void mutate("acknowledge", p.handoverId)}>Xác nhận đã nhận quản lý tài sản</button>
          <label>Lý do từ chối <textarea maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></label>
          <button disabled={reason.trim().length < 10} onClick={() => void mutate("reject", p.handoverId)}>Từ chối bàn giao</button></fieldset>}
      </section>)}
      <h3>Lịch sử bàn giao</h3>{data.historyIncomplete && <p>Chưa có thông tin: một số biên nhận cũ thiếu bằng chứng xác minh hoặc cần thêm quyền chi nhánh.</p>}{data.history.map(h => { const proof = evidence(h), decided = decision(h); return <article key={h.id}><h4>{h.number} — {displayCode(h.state)}</h4>
        <p>Đã nộp: {h.createdAt} UTC</p>{decided.decided_at && <p>Quyết định: {decided.decided_at} UTC; Trưởng bộ phận #{decided.to_user_id}. {decided.reason}</p>}
        <p>{h.conditionNote}</p><p>{proof.notes}</p>{proof.previous_handover_id && <p>Phiên bản trước #{proof.previous_handover_id}</p>}
        {proof.photo_ids?.filter(n => Number.isSafeInteger(n) && n > 0).map(n => <p key={n}><a href={`/api/asset-profiles/${id}/files/${n}`} download>Tải ảnh bàn giao #{n}</a></p>)}
        {data.canSubmit && h.canResubmit && <button disabled={busy || stale} onClick={() => { setPrevious(h.id); setCondition(h.conditionNote ?? ""); setNotes(proof.notes ?? ""); setFiles([]); }}>Gửi lại thành phiên bản mới</button>}
      </article>; })}
      <p>Trang {data.page} <button disabled={busy || page === 1} onClick={() => { setView(null); setPage(x => x - 1); }}>Trước</button> <button disabled={busy || page * data.pageSize >= data.total} onClick={() => { setView(null); setPage(x => x + 1); }}>Tiếp</button></p>
    </>}
  </section>;
}
