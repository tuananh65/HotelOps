import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";

type Row = { itemId: number; item: string; unit: string; available: string; minimumQty: string | null; rowVersion: string | null };
type Page = { warehouseId: number; warehouse: string; page: number; pageSize: number; totalCount: number; items: Row[] };
type Warehouse = { id: number; name: string; canConfigureMinimum: boolean };
type Draft = { minimum: string; version: string | null };

export function MinimumStockPage() {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const [params, setParams] = useSearchParams();
  const warehouse = params.get("warehouseId") ?? "", page = params.get("page") ?? "1";
  const [result, setResult] = useState<{ owner: AuthorityGeneration; key: string; warehouses: Warehouse[]; data: Page | null } | null>(null);
  const [drafts, setDrafts] = useState<Record<number, Draft>>({});
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [revision, setRevision] = useState(0);
  const write = useRef<AbortController | null>(null);
  const key = `${warehouse}:${page}`;
  const view = owner && result?.owner === owner && result.key === key ? result : null;
  useEffect(() => { setDrafts({}); setMessage(""); write.current?.abort(); setBusy(false); }, [owner, key]);
  useEffect(() => {
    setResult(null);
    if (!owner) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const fetchJson = async (url: string) => {
          const response = await fetch(url, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
          if (response.status === 401) actions.invalidateSession(owner);
          if (!response.ok) { const body = await response.json().catch(() => null); throw new Error(body?.detail ?? `Quyền truy cập chưa khả dụng (${response.status}).`); }
          return response.json();
        };
        const options = await fetchJson("/api/stock-balance");
        if (!Array.isArray(options.warehouses)) throw new Error("Phản hồi kho không hợp lệ.");
        const warehouses = (options.warehouses as Warehouse[]).filter(w => w.canConfigureMinimum === true);
        if (!warehouses.length) throw new Error("ACCESS_DENIED: Cần vai trò thủ kho và quyền ITEM_MASTER_MANAGE/INVENTORY_VIEW còn hiệu lực.");
        if (warehouse && !warehouses.some(w => String(w.id) === warehouse)) throw new Error("Kho ngoài phạm vi quản lý hiện tại.");
        const data: Page | null = warehouse ? await fetchJson(`/api/inventory-minimums?warehouseId=${encodeURIComponent(warehouse)}&page=${encodeURIComponent(page)}&pageSize=25`) : null;
        if (data && (String(data.warehouseId) !== warehouse || data.page !== Number(page) || !Array.isArray(data.items))) throw new Error("Phản hồi tồn tối thiểu không hợp lệ.");
        if (!abort.signal.aborted && current.current === owner) setResult({ owner, key, warehouses, data });
      } catch (e) { if (!abort.signal.aborted) { setDrafts({}); setMessage(e instanceof Error ? e.message : "Tồn kho tối thiểu chưa khả dụng."); } }
    })();
    const recheck = () => { setResult(null); setRevision(v => v + 1); };
    const timer = window.setInterval(recheck, 30000); window.addEventListener("focus", recheck);
    return () => { abort.abort(); window.clearInterval(timer); window.removeEventListener("focus", recheck); };
  }, [owner, key, warehouse, page, revision, actions]);
  useEffect(() => () => write.current?.abort(), []);
  const change = (name: string, value: string) => { const next = new URLSearchParams(params); next.set(name, value); if (name !== "page") next.delete("page"); setParams(next); };
  async function save() {
    if (!owner || !view?.data || busy || !Object.keys(drafts).length) return;
    const changes = Object.entries(drafts).map(([id, d]) => ({ itemId: Number(id), minimumQty: d.minimum.trim(), expectedRowVersion: d.version }));
    if (changes.some(c => !/^\d{1,15}(\.\d{1,3})?$/.test(c.minimumQty))) { setMessage("MSG03: Mức tối thiểu phải không âm và tối đa 3 chữ số thập phân."); return; }
    const abort = new AbortController(); write.current = abort; setBusy(true); setMessage("");
    try {
      const tokenResponse = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      const token = await tokenResponse.json();
      if (!tokenResponse.ok || token.headerName !== "X-CSRF-TOKEN" || typeof token.csrfToken !== "string" || !token.csrfToken) throw new Error("Chưa có mã bảo vệ phiên (CSRF).");
      if (current.current !== owner || abort.signal.aborted) return;
      const response = await fetch(`/api/inventory-minimums/${encodeURIComponent(warehouse)}`, { method: "PUT", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token.csrfToken }, body: JSON.stringify(changes) });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) { const body = await response.json().catch(() => null); throw new Error(body?.detail ?? `Lưu thất bại (${response.status}).`); }
      if (current.current === owner && !abort.signal.aborted) { setDrafts({}); setMessage("MSG08: Đã lưu tồn tối thiểu. Cảnh báo tồn thấp dùng giá trị này ở lần quét kế tiếp."); setRevision(v => v + 1); }
    } catch (e) { if (!abort.signal.aborted && current.current === owner) setMessage(e instanceof Error ? e.message : "Chưa xác định kết quả lưu. Tải lại trước khi thử lại."); }
    finally { if (write.current === abort) { write.current = null; setBusy(false); } }
  }
  return <section><h1>Định mức tồn kho tối thiểu</h1><Link to="/inventory/stock">Tồn kho và sổ kho</Link>
    <p>Nhập mức tối thiểu theo đơn vị cơ sở của vật tư. Để trống là chưa cấu hình; 0 là mức hợp lệ. Một mục sai hoặc cũ sẽ làm toàn bộ lần lưu bị từ chối.</p>
    {message && <p role="status">{message}</p>}
    {view && <><label>Kho <select disabled={busy || Object.keys(drafts).length > 0} value={warehouse} onChange={e => change("warehouseId", e.target.value)}><option value="">Chọn kho</option>
      {view.warehouses.map(w => <option key={w.id} value={w.id}>{w.name}</option>)}</select></label>
      {view.data && <><table><thead><tr><th>Vật tư</th><th>Đơn vị cơ sở</th><th>Khả dụng</th><th>Tối thiểu</th></tr></thead>
        <tbody>{view.data.items.map(r => <tr key={r.itemId}><td>{r.item}</td><td>{r.unit}</td><td>{r.available}</td>
          <td><input aria-label={`Mức tối thiểu cho ${r.item}`} disabled={busy} type="number" min="0" step="0.001" value={drafts[r.itemId]?.minimum ?? r.minimumQty ?? ""}
            onChange={e => setDrafts(d => ({ ...d, [r.itemId]: { minimum: e.target.value, version: d[r.itemId] ? d[r.itemId].version : r.rowVersion } }))} /></td></tr>)}</tbody></table>
        {view.data.items.length === 0 && <p>MSG10: Không có vật tư đang hoạt động.</p>}
        <button disabled={busy || Object.keys(drafts).length === 0} onClick={() => void save()}>Lưu định mức tối thiểu đã sửa</button>
        <button disabled={busy} onClick={() => { setDrafts({}); setRevision(v => v + 1); }}>Tải lại và bỏ chỉnh sửa</button>
        <button disabled={busy || view.data.page <= 1 || Object.keys(drafts).length > 0} onClick={() => change("page", String(view.data!.page - 1))}>Trước</button>
        <button disabled={busy || view.data.page * view.data.pageSize >= view.data.totalCount || Object.keys(drafts).length > 0} onClick={() => change("page", String(view.data!.page + 1))}>Tiếp</button>
      </>}
    </>}
  </section>;
}
