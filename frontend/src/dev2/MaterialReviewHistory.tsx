import { displayCode } from "../i18n/vi";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import type { Values } from "./contracts";
import { MaterialAvailabilityView } from "./MaterialAvailabilityView";

const request = createHttpClient();
type Page = { items: Values[]; total: number };
function decode(raw: unknown): Page | undefined {
  if (!isObject(raw) || !Array.isArray(raw.items) || !raw.items.every(isObject)
    || typeof raw.total !== "number" || !Number.isSafeInteger(raw.total) || raw.total < 0) return undefined;
  return raw as Page;
}

export function MaterialReviewHistory({ record }: { record: Values }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const items = (Array.isArray(record.lines) ? record.lines.filter(isObject) : [])
    .filter((l, index, all) => l.isActive === true && all.findIndex(x => x.isActive === true && x.itemId === l.itemId) === index);
  const [selected, setSelected] = useState("");
  const item = items.find(l => String(l.itemId) === selected) ?? items[0];
  const itemId = String(item?.itemId ?? ""), branch = String(record.branchId), requester = String(record.requesterId);
  const [revision, setRevision] = useState(0);
  const key = `${record.requestId}:${record.rowVersion}:${branch}:${requester}:${itemId}:${revision}`;
  const [loaded, setLoaded] = useState<{ owner: typeof owner; key: string; page?: Page }>();
  const [stock, setStock] = useState<{ owner: typeof owner; key: string; data?: Values }>();
  const stockKey = `${record.requestId}:${record.rowVersion}:${revision}`;
  useEffect(() => {
    setStock(undefined);
    if (owner === null || !Number.isSafeInteger(Number(record.requestId)) || Number(record.requestId) <= 0) return;
    const abort = new AbortController();
    void request({ method: "GET", url: `/api/material-requests/${encodeURIComponent(String(record.requestId))}/availability`,
      expectedStatus: 200, signal: abort.signal,
      decode: raw => isObject(raw) && Array.isArray(raw.warehouses) ? raw : undefined }).then(result => {
      if (abort.signal.aborted) return;
      setStock({ owner, key: stockKey, data: result.ok ? result.data : undefined });
      if (!result.ok && (result.error.kind === "stale" || result.error.kind === "http" && result.error.status === 401)) void actions.revalidate();
    });
    return () => abort.abort();
  }, [owner, stockKey, record.requestId, actions]);
  useEffect(() => {
    setLoaded(undefined);
    if (owner === null || ![branch, requester, itemId].every(v => /^[1-9]\d*$/.test(v) && Number.isSafeInteger(Number(v)))) return;
    const abort = new AbortController();
    const query = new URLSearchParams({ branchId: branch, requesterId: requester, itemId, page: "1", pageSize: "6" });
    void request({ method: "GET", url: `/api/material-requests?${query}`, expectedStatus: 200, decode, signal: abort.signal }).then(result => {
      if (abort.signal.aborted) return;
      setLoaded({ owner, key, page: result.ok ? result.data : undefined });
      if (!result.ok && (result.error.kind === "stale" || result.error.kind === "http" && result.error.status === 401)) void actions.revalidate();
    });
    return () => abort.abort();
  }, [owner, key, branch, requester, itemId, actions]);
  const current = loaded?.owner === owner && loaded.key === key ? loaded : undefined;
  const currentStock = stock?.owner === owner && stock.key === stockKey ? stock : undefined;
  const rows = current?.page?.items.filter(r => String(r.requestId) !== String(record.requestId)).slice(0, 5) ?? [];
  return <section aria-label="Yêu cầu vật tư gần đây để xét duyệt">
    <h3>Xem trước tồn kho hiện tại</h3>
    {currentStock?.data ? <MaterialAvailabilityView data={currentStock.data} />
      : <p role="status">{currentStock ? "Chưa có tồn kho. Cần quyền kho riêng và thẩm quyền xét duyệt hiện tại; chưa giữ chỗ tồn nào." : "Đang tải bản xem trước tồn kho được phép truy cập…"}</p>}
    <h3>Yêu cầu gần đây cho cùng vật tư</h3>
    <p>Tối đa 5 yêu cầu khác mới nhất của người này trong chi nhánh và quyền MR_VIEW hiện tại. Số lượng lịch sử không giữ chỗ tồn kho.</p>
    <label>Vật tư<select value={itemId} onChange={e => setSelected(e.target.value)}>
      {items.map(l => <option key={String(l.itemId)} value={String(l.itemId)}>{String(l.itemName ?? l.itemId)}</option>)}
    </select></label>
    <button type="button" onClick={() => setRevision(r => r + 1)}>Tải lại thông tin xét duyệt</button>
    {!current ? <p role="status">Đang tải yêu cầu gần đây…</p> : !current.page ? <p role="alert">Chưa tải được yêu cầu gần đây. Kiểm tra quyền xem và thử lại.</p>
      : rows.length === 0 ? <p>Không có yêu cầu phù hợp khác được phép xem.</p> : <table>
        <thead><tr><th>Yêu cầu</th><th>Đã tạo</th><th>Trạng thái</th><th>Số lượng theo đơn vị</th></tr></thead>
        <tbody>{rows.map(r => <tr key={String(r.requestId)}>
          <td><Link to={`/operations/material-requests/${encodeURIComponent(String(r.requestId))}`}>{String(r.requestNo)}</Link></td>
          <td>{String(r.createdAt)}</td><td>{displayCode(r.status)}</td>
          <td>{(Array.isArray(r.quantities) ? r.quantities.filter(isObject) : []).map(l => <p key={String(l.requestLineId)}>
            {String(l.itemName)}: Đã yêu cầu {String(l.requestedQty)}, Đã duyệt {String(l.approvedQty ?? "Chưa duyệt")}, Đã cấp {String(l.fulfilledQty)} {String(l.unitName)}
          </p>)}</td>
        </tr>)}</tbody>
      </table>}
    <Link to={`/operations/material-requests?branchId=${encodeURIComponent(branch)}&requesterId=${encodeURIComponent(requester)}&itemId=${encodeURIComponent(itemId)}`}>Mở tất cả yêu cầu phù hợp và bộ lọc</Link>
  </section>;
}
