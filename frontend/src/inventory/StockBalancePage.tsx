import { displayCode } from "../i18n/vi";
import { useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";

type Option = { id: number; name: string; branchId?: number; canReplenish?: boolean; canConfigureMinimum?: boolean };
type Row = { warehouseId: number; itemId: number; item: string; unit: string; available: number;
  outboundInTransit: number; inboundInTransit: number; minimum: number | null; belowMinimum: boolean };
type Movement = { movementId: number; documentId: number; document: string; type: string; at: string; quantity: number; runningBalance: number };
type Result = { snapshot: string; warehouses: Option[]; categories: Option[]; items: Row[]; totalCount: number;
  ledgerPage: number; ledgerPageSize: number; ledgerTotalCount: number;
  openingBalance: number | null; closingBalance: number | null; ledger: Movement[];
  filters: { page: number; pageSize: number; warehouseId: number | null; itemId: number | null; from: string; to: string } };

export function StockBalancePage() {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.pending === null ? state.authorityGeneration : null;
  const [params, setParams] = useSearchParams();
  const query = params.toString();
  const [result, setResult] = useState<{ owner: AuthorityGeneration; query: string; data: Result } | null>(null);
  const [error, setError] = useState("");
  const [refresh, setRefresh] = useState(0);
  const data = owner && result?.owner === owner && result.query === query ? result.data : null;
  useEffect(() => {
    setResult(null); setError("");
    if (!owner) return;
    const controller = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/stock-balance?${query}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: controller.signal });
        if (response.status === 401) { actions.invalidateSession(owner); return; }
        if (!response.ok) {
          const body = await response.json().catch(() => null);
          throw new Error(response.status === 403 ? "ACCESS_DENIED: Cần quyền kho và phạm vi chi nhánh phù hợp." : body?.detail ?? body?.message ?? `Đọc tồn kho thất bại (${response.status}).`);
        }
        const body = await response.json() as Result;
        if (!Array.isArray(body.items) || !Array.isArray(body.ledger) || !Array.isArray(body.warehouses) || !Array.isArray(body.categories)
          || !Number.isSafeInteger(body.totalCount) || body.totalCount < 0 || body.filters?.page !== Number(params.get("page") ?? 1)
          || body.filters.warehouseId !== (params.get("warehouseId") ? Number(params.get("warehouseId")) : null)
          || body.filters.itemId !== (params.get("itemId") ? Number(params.get("itemId")) : null)) throw new Error("Phản hồi tồn kho bất thường; tải lại.");
        if (!controller.signal.aborted) setResult({ owner, query, data: body });
      } catch (e) { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : "Tồn kho chưa khả dụng."); }
    })();
    const recheck = () => { setResult(null); setRefresh(v => v + 1); };
    window.addEventListener("focus", recheck);
    const timer = window.setInterval(recheck, 30000);
    return () => { controller.abort(); window.removeEventListener("focus", recheck); window.clearInterval(timer); };
  }, [owner, query, refresh, actions, params]);
  const change = (name: string, value: string) => {
    const next = new URLSearchParams(params); value ? next.set(name, value) : next.delete(name);
    if (name !== "page" && name !== "ledgerPage") { next.delete("page"); next.delete("ledgerPage"); }
    setParams(next);
  };
  const ledger = (row: Row) => {
    const next = new URLSearchParams(params); next.set("warehouseId", String(row.warehouseId)); next.set("itemId", String(row.itemId)); next.delete("page"); next.delete("ledgerPage"); setParams(next);
  };
  return <section aria-label="Tồn kho và sổ biến động">
    <h1>Tồn kho và sổ biến động</h1>
    <p>Số lượng theo đơn vị cơ sở của vật tư. Hàng đang vận chuyển đến và đi được tách riêng; không phải tồn khả dụng.</p>
    <Link to="/inventory/items">Danh mục vật tư</Link>
    {!owner ? <p role="status">Xác minh phiên đăng nhập để xem tồn kho.</p> : <>
      <label>Kho <select value={params.get("warehouseId") ?? ""} onChange={e => change("warehouseId", e.target.value)}>
        <option value="">Tất cả kho được phép truy cập</option>{data?.warehouses.map(w => <option key={w.id} value={w.id}>{w.name}</option>)}
      </select></label>
      <label>Mã vật tư <input type="number" min="1" value={params.get("itemId") ?? ""} onChange={e => change("itemId", e.target.value)} /></label>
      <label>Loại / nhóm <select value={params.get("categoryId") ?? ""} onChange={e => change("categoryId", e.target.value)}>
        <option value="">Tất cả loại</option>{data?.categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
      </select></label>
      <label><input type="checkbox" checked={params.get("belowMinimum") === "true"} onChange={e => change("belowMinimum", e.target.checked ? "true" : "")} />Dưới mức tối thiểu</label>
      <label>từ (UTC) <input type="date" value={params.get("from") ?? data?.filters.from ?? ""} onChange={e => change("from", e.target.value)} /></label>
      <label>Đến (UTC) <input type="date" value={params.get("to") ?? data?.filters.to ?? ""} onChange={e => change("to", e.target.value)} /></label>
      <button onClick={() => { setResult(null); setRefresh(v => v + 1); }}>Tải lại</button>
      {error && <p role="alert">{error}</p>}
      {!data && !error && <p role="status">Đang tải tồn kho hiện tại được phép truy cập…</p>}
      {data && <>
        {data.warehouses.some(w => w.canConfigureMinimum) && <Link to={`/inventory/minimums${data.filters.warehouseId ? `?warehouseId=${data.filters.warehouseId}` : ""}`}>Cấu hình định mức tồn kho tối thiểu</Link>}
        <p>Bản chụp dữ liệu {data.snapshot}; {data.totalCount} cặp tồn kho phù hợp.</p>
        {data.items.length === 0 && <p>MSG10: Không có cặp tồn kho phù hợp bộ lọc.</p>}
        <table><thead><tr><th>Kho</th><th>Vật tư / đơn vị</th><th>Khả dụng</th><th>Hàng đang vận chuyển đi</th><th>Hàng đang vận chuyển đến</th><th>Tối thiểu</th><th>Sổ kho</th></tr></thead>
          <tbody>{data.items.map(r => <tr key={`${r.warehouseId}:${r.itemId}`}>
            <td>{data.warehouses.find(w => w.id === r.warehouseId)?.name}</td><td>{r.item} / {r.unit}</td>
            <td>{r.available}{r.belowMinimum && <strong> — Dưới mức tối thiểu</strong>}</td><td>{r.outboundInTransit}</td><td>{r.inboundInTransit}</td><td>{r.minimum ?? "Chưa cấu hình"}</td>
            <td><button onClick={() => ledger(r)}>Xem sổ kho</button>
              {r.belowMinimum && data.warehouses.find(w => w.id === r.warehouseId)?.canReplenish === true &&
                <Link to={`/operations/replenishments?action=A004A-056&branchId=${data.warehouses.find(w => w.id === r.warehouseId)?.branchId}&warehouseId=${r.warehouseId}&itemId=${r.itemId}&shortageSource=low-stock`}>Tạo bổ sung vật tư</Link>}
            </td></tr>)}</tbody></table>
        <button disabled={data.filters.page <= 1} onClick={() => change("page", String(data.filters.page - 1))}>Trước</button>
        <button disabled={data.filters.page * data.filters.pageSize >= data.totalCount} onClick={() => change("page", String(data.filters.page + 1))}>Tiếp</button>
        {data.openingBalance !== null && <>
          <h2>Sổ biến động đã ghi nhận</h2><p>{data.filters.from} – {data.filters.to} UTC. Số dư đầu kỳ {data.openingBalance}; đóng {data.closingBalance}.</p>
          {data.ledgerTotalCount === 0 && <p>MSG10: Không có biến động trong kỳ. Giữ số dư đầu kỳ.</p>}
          <table><thead><tr><th>lúc (UTC)</th><th>Chứng từ</th><th>Loại</th><th>Số lượng</th><th>Số dư lũy kế</th></tr></thead>
            <tbody>{data.ledger.map(m => <tr key={m.movementId}><td>{m.at}</td><td><Link to={`/inventory/documents/${m.documentId}`}>{m.document}</Link></td>
              <td>{displayCode(m.type)}</td><td>{m.quantity}</td><td>{m.runningBalance}</td></tr>)}</tbody></table>
          <p>{data.ledgerTotalCount} biến động trong kỳ; trang {data.ledgerPage}. Opening and closing balances cover the full period.</p>
          <button disabled={data.ledgerPage <= 1} onClick={() => change("ledgerPage", String(data.ledgerPage - 1))}>Biến động kho trước</button>
          <button disabled={data.ledgerPage * data.ledgerPageSize >= data.ledgerTotalCount} onClick={() => change("ledgerPage", String(data.ledgerPage + 1))}>Biến động kho tiếp theo</button>
          <p>Mở hoặc đảo chứng từ cần quyền riêng tương ứng với chứng từ.</p>
        </>}
      </>}
    </>}
  </section>;
}

type Document = { documentId: number; documentNo: string; documentType: string; status: string; sourceWarehouseId: number | null;
  destinationWarehouseId: number | null; documentDate: string; postedAt: string | null; rowVersion: string;
  lines: { lineId: number; itemId: number; uomId: number; actualQty: number; baseQty: number; receivedQty: number | null; damagedQty: number | null; missingQty: number | null; inTransitQty: number | null }[] };

export function StockDocumentPage() {
  const { id } = useParams();
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.pending === null ? state.authorityGeneration : null;
  const [loaded, setLoaded] = useState<{ owner: AuthorityGeneration; id: string; data: Document } | null>(null);
  const [error, setError] = useState("");
  const [revision, setRevision] = useState(0);
  const data = owner && loaded?.owner === owner && loaded.id === id ? loaded.data : null;
  useEffect(() => {
    setLoaded(null); setError("");
    if (!owner || !id || !/^[1-9]\d*$/.test(id)) return;
    const controller = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/stock-balance/documents/${encodeURIComponent(id)}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: controller.signal });
        if (response.status === 401) { actions.invalidateSession(owner); return; }
        if (!response.ok) throw new Error(`Chứng từ chưa khả dụng (${response.status}); cần quyền hiện tại với cả hai kho để điều chuyển.`);
        const body = await response.json() as Document;
        if (String(body.documentId) !== id || !Array.isArray(body.lines) || typeof body.rowVersion !== "string") throw new Error("Phản hồi chứng từ bất thường.");
        if (!controller.signal.aborted) setLoaded({ owner, id, data: body });
      } catch (e) { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : "Chứng từ chưa khả dụng."); }
    })();
    const recheck = () => { setLoaded(null); setRevision(v => v + 1); };
    window.addEventListener("focus", recheck); const timer = window.setInterval(recheck, 30000);
    return () => { controller.abort(); window.removeEventListener("focus", recheck); window.clearInterval(timer); };
  }, [owner, id, actions, revision]);
  return <section><h1>Chứng từ kho</h1><Link to="/inventory/stock">Tồn kho và sổ kho</Link>
    {error && <p role="alert">{error}</p>}
    {data && <><h2>{data.documentNo}</h2><p>{displayCode(data.documentType)} — {displayCode(data.status)}; Ngày chứng từ {data.documentDate}; đã ghi sổ {data.postedAt ?? "Chưa ghi sổ"}.</p>
      <p>Kho nguồn {data.sourceWarehouseId ?? "N/A"}; Đích {data.destinationWarehouseId ?? "N/A"}.</p>
      <p>Mã chứng từ đã chọn: {data.documentId}. <Link to={`/inventory/documents/${data.documentId}/reverse`}>Kiểm tra điều kiện đảo / đảo chứng từ</Link></p>
      <table><thead><tr><th>Vật tư</th><th>Đơn vị</th><th>Thực tế</th><th>Số lượng theo đơn vị cơ sở</th><th>Đã nhận</th><th>Hư hỏng</th><th>Thiếu</th><th>Đang vận chuyển</th></tr></thead>
        <tbody>{data.lines.map(l => <tr key={l.lineId}><td>{l.itemId}</td><td>{l.uomId}</td><td>{l.actualQty}</td><td>{l.baseQty}</td><td>{l.receivedQty ?? "N/A"}</td><td>{l.damagedQty ?? "N/A"}</td><td>{l.missingQty ?? "N/A"}</td><td>{l.inTransitQty ?? "N/A"}</td></tr>)}</tbody></table>
    </>}
  </section>;
}
