import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import type { Values } from "./contracts";

const http = createHttpClient();
type Page = { items: Values[]; total: number };
const positive = (v: unknown) => Number.isSafeInteger(Number(v)) && Number(v) > 0;
function decode(v: unknown): Page | undefined {
  return isObject(v) && Array.isArray(v.items) && v.items.every(isObject)
    && typeof v.total === "number" && Number.isSafeInteger(v.total) && v.total >= 0 ? v as Page : undefined;
}
export function replenishmentCreateReady(v: Values): boolean {
  return positive(v.branchId) && positive(v.warehouseId) && v.shortageScope === `${v.branchId}:${v.warehouseId}`
    && Array.isArray(v.lines) && v.lines.length > 0 && v.lines.every(l => isObject(l)
      && positive(l.itemId) && Number.isFinite(Number(l.requestedBaseQty)) && Number(l.requestedBaseQty) > 0);
}
export function ReplenishmentCreateFields({ values, onChange }: { values: Values; onChange: (v: Values) => void }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const [source, setSource] = useState("low-stock"), [page, setPage] = useState(1), [revision, setRevision] = useState(0);
  const branch = String(values.branchId ?? ""), warehouse = String(values.warehouseId ?? "");
  const key = `${branch}:${warehouse}:${source}:${page}:${revision}`;
  const [loaded, setLoaded] = useState<{ owner: typeof owner; key: string; data?: Page }>();
  useEffect(() => {
    setLoaded(undefined);
    if (owner === null || !positive(branch) || !positive(warehouse)) return;
    const abort = new AbortController();
    void http({ method: "GET", url: `/api/replenishment-shortages/${source}?branchId=${branch}&warehouseId=${warehouse}&page=${page}&pageSize=20`,
      expectedStatus: 200, decode, signal: abort.signal }).then(result => {
      if (abort.signal.aborted) return;
      setLoaded({ owner, key, data: result.ok ? result.data : undefined });
      if (!result.ok && (result.error.kind === "stale" || result.error.kind === "http" && result.error.status === 401)) void actions.revalidate();
    });
    return () => abort.abort();
  }, [owner, branch, warehouse, source, page, key, actions]);
  const current = loaded?.owner === owner && loaded.key === key ? loaded : undefined;
  const lines = Array.isArray(values.lines) ? values.lines.filter(isObject) : [];
  function reset(next: Values) { onChange({ ...next, lines: [], shortageScope: "" }); setPage(1); }
  function select(c: Values) {
    if (Number(c.branchId) !== Number(branch) || Number(c.warehouseId) !== Number(warehouse) || !positive(c.itemId)
      || c.existingActiveReplenishmentId != null) return;
    const low = source === "low-stock", id = low ? c.thresholdId : c.requestLineId;
    const quantity = Number(low ? c.deficitToMinimum : c.remainingBaseQty);
    if (!positive(id) || !Number.isFinite(quantity) || quantity <= 0) return;
    onChange({ ...values, shortageScope: `${branch}:${warehouse}`, lines: [...lines.filter(l => l.itemId !== c.itemId), {
      lineNo: lines.reduce((max, l) => Math.max(max, Number(l.lineNo)), 0) + 1, itemId: c.itemId,
      sourceType: low ? "LOW_STOCK" : "MATERIAL_REQUEST_SHORTAGE", sourceThresholdId: low ? id : null,
      sourceMaterialRequestLineId: low ? null : id, requestedBaseQty: quantity, note: null,
    }] });
  }
  return <section aria-label="Create replenishment from shortages">
    <label>Branch ID<input required type="number" min="1" step="1" value={branch} onChange={e => reset({ ...values, branchId: e.target.value })} /></label>
    <label>Receiving warehouse ID<input required type="number" min="1" step="1" value={warehouse} onChange={e => reset({ ...values, warehouseId: e.target.value })} /></label>
    <label>Supplier (optional)<input maxLength={200} value={String(values.supplierName ?? "")} onChange={e => onChange({ ...values, supplierName: e.target.value })} /></label>
    <label>Expected date (optional)<input type="date" value={String(values.expectedOn ?? "")} onChange={e => onChange({ ...values, expectedOn: e.target.value })} /></label>
    <label>Shortage source<select value={source} onChange={e => { setSource(e.target.value); setPage(1); }}>
      <option value="low-stock">Below minimum stock</option><option value="material-requests">Approved material requests</option>
    </select></label>
    <p>Material demand includes only requests you can currently read. Quantities use base units; these figures do not reserve stock.</p>
    <button type="button" onClick={() => { reset(values); setRevision(r => r + 1); }}>Refresh shortages and clear selection</button>
    {!current?.data && <p role="status">{current ? "Shortages unavailable. Check your current grants and warehouse scope." : "Enter branch and warehouse to load authorized shortages."}</p>}
    {current?.data?.items.length === 0 && <p>No visible shortages.</p>}
    {current?.data?.items.map((c, index) => <div key={index}>
      Item {String(c.itemId)}, base unit {String(c.baseUomId)}{c.requestNo != null && `, request ${String(c.requestNo)}`}:
      {" "}{String(source === "low-stock" ? c.deficitToMinimum : c.remainingBaseQty)}
      <button type="button" disabled={c.existingActiveReplenishmentId != null} onClick={() => select(c)}>
        {c.existingActiveReplenishmentId != null ? "Already covered" : "Select item"}</button>
    </div>)}
    <button type="button" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</button>
    <button type="button" disabled={!current?.data || page * 20 >= current.data.total} onClick={() => setPage(p => p + 1)}>Next</button>
    {lines.map(l => <label key={String(l.itemId)}>Item {String(l.itemId)} requested base quantity
      <input type="number" required min="0.001" step="0.001" value={String(l.requestedBaseQty)} onChange={e => onChange({ ...values,
        lines: lines.map(x => x.itemId === l.itemId ? { ...x, requestedBaseQty: e.target.value } : x) })} />
      <button type="button" onClick={() => onChange({ ...values, lines: lines.filter(x => x.itemId !== l.itemId) })}>Remove</button>
    </label>)}
    <p>The server rechecks shortage, source access and duplicate coverage atomically when creating the request.</p>
  </section>;
}
