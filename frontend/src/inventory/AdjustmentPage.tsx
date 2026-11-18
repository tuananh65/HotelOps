import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Document = { documentId: number; documentNo: string; status: string; destinationWarehouseId: number; reason: string; rowVersion: string; canPost: boolean; lines: { itemName: string; unit: string; delta: string; currentQty: string; resultingQty: string | null }[] };
type Workspace = { warehouses: { id: number; name: string }[]; items: { id: number; name: string; unit: string }[]; documents: Document[]; count: number; pageSize: number };
export function AdjustmentPage() {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const [loaded, setLoaded] = useState<{ owner: typeof owner; page: number; data: Workspace }>();
  const [page, setPage] = useState(1), [revision, setRevision] = useState(0);
  const data = loaded?.owner === owner && loaded.page === page ? loaded.data : null;
  const [warehouse, setWarehouse] = useState(""), [date, setDate] = useState(""), [reason, setReason] = useState("");
  const [lines, setLines] = useState([{ itemId: "", deltaBaseQty: "" }]);
  const [message, setMessage] = useState(""), [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false);
  const writer = useRef<AbortController | null>(null);
  useEffect(() => { setWarehouse(""); setReason(""); setDate(""); setLines([{ itemId: "", deltaBaseQty: "" }]); }, [owner]);
  useEffect(() => {
    setLoaded(undefined); if (!owner) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/stocktake-workspace/adjustments?page=${page}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        if (response.status === 401) actions.invalidateSession(owner);
        const value = await response.json();
        if (!response.ok) throw new Error(value.detail ?? `Không có quyền hoặc chưa thể điều chỉnh (${response.status}).`);
        if (!abort.signal.aborted && current.current === owner) setLoaded({ owner, page, data: value });
      } catch (e) { if (!abort.signal.aborted) setMessage(e instanceof Error ? e.message : "Điều chỉnh chưa khả dụng."); }
    })();
    const refresh = () => { if (!writer.current) { setLoaded(undefined); setRevision(v => v + 1); } };
    const timer = setInterval(refresh, 30000); window.addEventListener("focus", refresh);
    return () => { abort.abort(); clearInterval(timer); window.removeEventListener("focus", refresh); };
  }, [owner, page, revision, actions]);
  useEffect(() => () => writer.current?.abort(), [owner]);
  async function save(document?: Document) {
    if (!owner || !data || busy || uncertain || writer.current) return;
    if (!document && (!date || reason.trim().length < 10 || !data.warehouses.some(w => String(w.id) === warehouse)
      || lines.some(l => !data.items.some(i => String(i.id) === l.itemId) || !/^-?\d+(\.\d{1,3})?$/.test(l.deltaBaseQty) || !/[1-9]/.test(l.deltaBaseQty)))) {
      setMessage("Chọn kho được phép, ngày và vật tư; nhập số lượng tăng/giảm khác 0 và lý do điều chỉnh ít nhất 10 ký tự."); return;
    }
    if (document && (!document.canPost || !window.confirm(`Ghi sổ ${document.documentNo}? Thao tác sẽ đổi số dư tồn kho.`))) return;
    const abort = new AbortController(); writer.current = abort; setBusy(true); let sent = false;
    try {
      const response = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      const csrf = await response.json();
      if (!response.ok || csrf.headerName !== "X-CSRF-TOKEN" || !csrf.csrfToken) throw new Error("Chưa có bảo vệ phiên (CSRF).");
      if (abort.signal.aborted || current.current !== owner) return;
      sent = true;
      const result = await fetch(document ? `/api/inventory/adjustments/${document.documentId}/post` : "/api/inventory/adjustments", {
        method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken },
        body: JSON.stringify(document ? { expectedRowVersion: document.rowVersion } : { warehouseId: Number(warehouse), documentDate: date, reason,
          lines: lines.map(l => ({ itemId: Number(l.itemId), deltaBaseQty: l.deltaBaseQty })) }) });
      if (result.status === 401) actions.invalidateSession(owner);
      const value = await result.json();
      if (!result.ok) { if (result.status < 500) sent = false; if ([401, 403].includes(result.status)) setLoaded(undefined); throw new Error(value.detail ?? `Điều chỉnh thất bại (${result.status}).`); }
      if (!abort.signal.aborted && current.current === owner) {
        setMessage(document ? "MSG08: Đã ghi sổ điều chỉnh." : "Đã lưu đề xuất điều chỉnh. Cần quản lý vận hành khác có quyền ghi sổ.");
        setLoaded(undefined); setRevision(v => v + 1);
        if (!document) { setLines([{ itemId: "", deltaBaseQty: "" }]); setReason(""); }
      }
    } catch (e) { if (sent) setUncertain(true); if (!abort.signal.aborted) setMessage(`${e instanceof Error ? e.message : "Điều chỉnh thất bại."}${sent ? "Chưa xác định kết quả. Mở lại và kiểm tra trước khi thử lại." : ""}`); }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  return <main><h1>Điều chỉnh tồn kho</h1><Link to="/inventory/stocktakes">Kiểm kê</Link><p role="status">{message}</p>
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Tải lại</button>
    {data && <><form onSubmit={e => { e.preventDefault(); void save(); }}><fieldset disabled={busy || uncertain}><legend>Điều chỉnh lỗi mới</legend>
      <label>Kho<select required value={warehouse} onChange={e => setWarehouse(e.target.value)}><option value="">Chọn kho</option>{data.warehouses.map(w => <option key={w.id} value={w.id}>{w.name}</option>)}</select></label>
      <label>Ngày chứng từ<input required type="date" value={date} onChange={e => setDate(e.target.value)} /></label>
      <label>Lý do điều chỉnh<textarea required minLength={10} value={reason} onChange={e => setReason(e.target.value)} /></label>
      {lines.map((line, index) => <div key={index}><label>Vật tư<select required value={line.itemId} onChange={e => setLines(rows => rows.map((l, i) => i === index ? { ...l, itemId: e.target.value } : l))}><option value="">Chọn vật tư</option>{data.items.map(i => <option key={i.id} value={i.id}>{i.name} ({displayCode(i.unit)})</option>)}</select></label>
        <label>Số lượng cơ sở có dấu tăng/giảm<input required inputMode="decimal" value={line.deltaBaseQty} onChange={e => setLines(rows => rows.map((l, i) => i === index ? { ...l, deltaBaseQty: e.target.value } : l))} /></label>
        <button type="button" disabled={lines.length === 1} onClick={() => setLines(rows => rows.filter((_, i) => i !== index))}>Xóa</button></div>)}
      <button type="button" onClick={() => setLines(rows => [...rows, { itemId: "", deltaBaseQty: "" }])}>Thêm vật tư</button><button>Lưu đề xuất</button>
    </fieldset></form>
      {data.documents.map(d => <section key={d.documentId}><h2>{d.documentNo} � {displayCode(d.status)}</h2><p>{data.warehouses.find(w => w.id === d.destinationWarehouseId)?.name}: {d.reason}</p>
        <ul>{d.lines.map((l, i) => <li key={i}>{l.itemName}: {l.delta} {l.unit}; Hiện tại: {l.currentQty}{l.resultingQty != null && `; sau mọi chênh lệch đề xuất: ${l.resultingQty}`}</li>)}</ul>
        {d.canPost && <button disabled={busy || uncertain} onClick={() => void save(d)}>Phê duyệt và ghi sổ</button>}</section>)}
      <p>{data.count} điều chỉnh được phép xem; trang {page}</p><button disabled={busy || page === 1} onClick={() => setPage(p => p - 1)}>Trước</button><button disabled={busy || page * data.pageSize >= data.count} onClick={() => setPage(p => p + 1)}>Tiếp</button>
    </>}
  </main>;
}
