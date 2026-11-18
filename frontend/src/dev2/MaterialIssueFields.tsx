import { displayCode } from "../i18n/vi";
﻿import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { createHttpClient, isObject } from "../auth/http";
import { useAuth } from "../auth/AuthProvider";
import type { Values } from "./contracts";

const request = createHttpClient();
type Preview = { requestId: number; warehouseId: number; rowVersion: string; warehouses: Values[] };
function decode(raw: unknown): Preview | undefined {
  if (!isObject(raw) || !Number.isSafeInteger(raw.requestId) || !Number.isSafeInteger(raw.warehouseId)
    || typeof raw.rowVersion !== "string" || !Array.isArray(raw.warehouses)
    || raw.warehouses.some(w => !isObject(w) || !Number.isSafeInteger(w.warehouseId) || !Array.isArray(w.lines)
      || w.lines.some(l => !isObject(l) || !Number.isSafeInteger(l.requestLineId)
        || !(l.fulfillableQty === null || typeof l.fulfillableQty === "number" && Number.isFinite(l.fulfillableQty) && l.fulfillableQty >= 0)))) return undefined;
  return raw as Preview;
}

export function materialIssueReady(record: Values, actor: string): boolean {
  const cycle = Array.isArray(record.cycles) ? record.cycles.filter(isObject).sort((a, b) => Number(b.revisionNo) - Number(a.revisionNo))[0] : undefined;
  return record.isNewWorkflow === true && record.mutationEligible === true && typeof record.rowVersion === "string"
    && ["APPROVED", "PARTIALLY_ISSUED"].includes(String(record.rawStatus)) && String(record.requesterId) !== actor
    && cycle?.status === "APPROVED" && String(cycle.assignedApproverUserId) !== actor;
}

export function MaterialIssueFields({ record, values, onChange }: { record: Values; values: Values; onChange: (value: Values) => void }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const id = String(record.requestId ?? ""), version = String(record.rowVersion ?? ""), warehouse = String(record.sourceWarehouseId ?? "");
  const key = `${id}:${version}:${warehouse}`;
  const [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{ owner: typeof owner; key: string; data?: Preview; message: string }>();
  useEffect(() => {
    setLoaded(undefined);
    if (!owner || !/^[1-9]\d*$/.test(id) || !Number.isSafeInteger(Number(id)) || !version) return;
    const abort = new AbortController();
    void request({ method: "GET", url: `/api/material-requests/${id}/availability`, expectedStatus: 200, decode, signal: abort.signal }).then(result => {
      if (abort.signal.aborted) return;
      const match = result.ok && String(result.data.requestId) === id && result.data.rowVersion === version
        && String(result.data.warehouseId) === warehouse;
      setLoaded({ owner, key, data: match ? result.data : undefined, message: match ? ""
        : "Chưa xác nhận số lượng khả dụng. Tải lại yêu cầu và tồn khả dụng trước khi nhập cấp một phần." });
      if (!result.ok && (result.error.kind === "stale" || result.error.kind === "http" && result.error.status === 401)) void actions.revalidate();
    });
    return () => abort.abort();
  }, [owner, id, version, warehouse, key, revision, actions]);
  const current = loaded?.owner === owner && loaded.key === key ? loaded : undefined;
  const stock = current?.data?.warehouses.find(w => String(w.warehouseId) === warehouse);
  const preview = stock && Array.isArray(stock.lines) ? stock.lines.filter(isObject) : [];
  const lines = Array.isArray(record.lines) ? record.lines.filter(isObject).filter(l => l.isActive === true && Number(l.openFulfillableQty) > 0) : [];
  const entered = Array.isArray(values.lines) ? values.lines.filter(isObject) : [];
  return <section aria-label="Cấp vật tư đã duyệt">
    <p>Chỉ cấp số lượng đã duyệt còn lại. Khả dụng không phải giữ chỗ; tồn kho được kiểm tra lại khi lưu.</p>
    <label>Kho cấp phát
      <select required value={String(values.warehouseId ?? "")} onChange={event => onChange({ ...values, warehouseId: event.target.value })}>
        <option value="">Chọn kho được duyệt</option>
        <option value={warehouse}>Kho của yêu cầu đã duyệt {displayCode(warehouse)}</option>
      </select>
    </label>
    <p>Đổi kho đã duyệt cần cập nhật và duyệt lại yêu cầu.</p>
    <label>Tên người nhận
      <input required maxLength={200} value={String(values.receiverName ?? "")} onChange={event => onChange({ ...values, receiverName: event.target.value })} />
    </label>
    <button type="button" onClick={() => setRevision(v => v + 1)}>Tải lại tồn kho cấp phát</button>
    <p role="status">{current?.message ?? "Đang tải khả dụng hiện tại…"}</p>
    {lines.map(line => {
      const available = preview.find(p => String(p.requestLineId) === String(line.requestLineId));
      const maximum = typeof available?.fulfillableQty === "number" ? Math.min(Number(line.openFulfillableQty), available.fulfillableQty) : 0;
      return <div key={String(line.requestLineId)}>
      <p>{String(line.itemName ?? `Vật tư ${line.itemId}`)}: Đã duyệt {String(line.approvedQty)}, Đã cấp {String(line.fulfilledQty)}, Còn lại {String(line.openFulfillableQty)} {String(line.unitName ?? "")}.</p>
      <p>Có thể cấp ngay: {available?.fulfillableQty == null ? "Chưa có thông tin" : `${maximum} ${line.unitName ?? ""}`}.</p>
      {available?.unavailableReason != null && <p>{String(available.unavailableReason)}</p>}
      {available?.sharedItemStock === true && <p>Tồn kho dùng chung với các dòng khác của vật tư này. Tổng số lượng được kiểm tra khi ghi sổ.</p>}
      {available?.fulfillableQty != null && maximum < Number(line.openFulfillableQty)
        && <p><strong>Tồn kho không đủ cấp toàn bộ phần còn lại.</strong> Chọn cấp một phần hoặc yêu cầu bổ sung.
          {record.branchId != null && record.sourceWarehouseId != null && <Link to={`/operations/replenishments?action=A004A-056&branchId=${encodeURIComponent(String(record.branchId))}&warehouseId=${encodeURIComponent(String(record.sourceWarehouseId))}&shortageSource=material-requests&sourceRequestId=${encodeURIComponent(String(record.requestId))}`}>Tạo yêu cầu bổ sung cho phiếu này</Link>}
          {values.mode === "FullRemaining" && <button type="button" onClick={() => onChange({ ...values, mode: "Partial", lines: [] })}>Nhập số lượng cấp một phần</button>}</p>}
      {values.mode === "Partial" && <label>Số lượng cấp (nhập 0 để bỏ qua dòng này)
        <input type="number" min="0" max={maximum} step="0.001" required
          value={String(entered.find(l => String(l.requestLineId) === String(line.requestLineId))?.quantity ?? 0)}
          onChange={event => onChange({ ...values, lines: lines.map(l => ({ requestLineId: l.requestLineId,
            quantity: l.requestLineId === line.requestLineId ? event.target.value
              : entered.find(e => String(e.requestLineId) === String(l.requestLineId))?.quantity ?? 0 })) })} />
      </label>}
    </div>; })}
    {values.mode === "FullRemaining" && <p>Toàn bộ số lượng còn lại ở trên sẽ được cấp cùng lúc; nếu không đủ điều kiện, toàn bộ giao dịch thất bại.</p>}
  </section>;
}
