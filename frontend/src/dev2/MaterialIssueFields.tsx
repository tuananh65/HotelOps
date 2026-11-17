import { useEffect, useState } from "react";
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
        : "Current availability could not be confirmed. Reload the request and availability before entering a partial issue." });
      if (!result.ok && (result.error.kind === "stale" || result.error.kind === "http" && result.error.status === 401)) void actions.revalidate();
    });
    return () => abort.abort();
  }, [owner, id, version, warehouse, key, revision, actions]);
  const current = loaded?.owner === owner && loaded.key === key ? loaded : undefined;
  const stock = current?.data?.warehouses.find(w => String(w.warehouseId) === warehouse);
  const preview = stock && Array.isArray(stock.lines) ? stock.lines.filter(isObject) : [];
  const lines = Array.isArray(record.lines) ? record.lines.filter(isObject).filter(l => l.isActive === true && Number(l.openFulfillableQty) > 0) : [];
  const entered = Array.isArray(values.lines) ? values.lines.filter(isObject) : [];
  return <section aria-label="Issue approved materials">
    <p>Only the remaining approved quantities can be issued. Availability is not a reservation; stock is checked again when saving.</p>
    <label>Issuing warehouse
      <select required value={String(values.warehouseId ?? "")} onChange={event => onChange({ ...values, warehouseId: event.target.value })}>
        <option value="">Select approved warehouse</option>
        <option value={warehouse}>Approved request warehouse {warehouse}</option>
      </select>
    </label>
    <p>Changing the approved warehouse requires updating and reapproving the request.</p>
    <label>Receiver name
      <input required maxLength={200} value={String(values.receiverName ?? "")} onChange={event => onChange({ ...values, receiverName: event.target.value })} />
    </label>
    <button type="button" onClick={() => setRevision(v => v + 1)}>Refresh issuing warehouse availability</button>
    <p role="status">{current?.message ?? "Loading current availability…"}</p>
    {lines.map(line => {
      const available = preview.find(p => String(p.requestLineId) === String(line.requestLineId));
      const maximum = typeof available?.fulfillableQty === "number" ? Math.min(Number(line.openFulfillableQty), available.fulfillableQty) : 0;
      return <div key={String(line.requestLineId)}>
      <p>{String(line.itemName ?? `Material ${line.itemId}`)}: approved {String(line.approvedQty)}, issued {String(line.fulfilledQty)}, remaining {String(line.openFulfillableQty)} {String(line.unitName ?? "")}.</p>
      <p>Fulfillable now: {available?.fulfillableQty == null ? "N/A" : `${maximum} ${line.unitName ?? ""}`}.</p>
      {available?.unavailableReason != null && <p>{String(available.unavailableReason)}</p>}
      {available?.sharedItemStock === true && <p>Stock is shared with other lines of this material. Combined quantities are checked when posting.</p>}
      {available?.fulfillableQty != null && maximum < Number(line.openFulfillableQty)
        && <p><strong>Insufficient stock for the full remainder.</strong> Choose Partial or request replenishment.
          {record.branchId != null && record.sourceWarehouseId != null && <Link to={`/operations/replenishments?action=A004A-056&branchId=${encodeURIComponent(String(record.branchId))}&warehouseId=${encodeURIComponent(String(record.sourceWarehouseId))}&shortageSource=material-requests&sourceRequestId=${encodeURIComponent(String(record.requestId))}`}>Create replenishment for this request</Link>}
          {values.mode === "FullRemaining" && <button type="button" onClick={() => onChange({ ...values, mode: "Partial", lines: [] })}>Enter partial quantities</button>}</p>}
      {values.mode === "Partial" && <label>Quantity to issue (zero skips this line)
        <input type="number" min="0" max={maximum} step="0.001" required
          value={String(entered.find(l => String(l.requestLineId) === String(line.requestLineId))?.quantity ?? 0)}
          onChange={event => onChange({ ...values, lines: lines.map(l => ({ requestLineId: l.requestLineId,
            quantity: l.requestLineId === line.requestLineId ? event.target.value
              : entered.find(e => String(e.requestLineId) === String(l.requestLineId))?.quantity ?? 0 })) })} />
      </label>}
    </div>; })}
    {values.mode === "FullRemaining" && <p>All remaining quantities shown above will be issued together, or the transaction will fail.</p>}
  </section>;
}
