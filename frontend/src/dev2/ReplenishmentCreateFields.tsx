import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import type { Values } from "./contracts";
import { replenishmentBaseQuantity } from "./replenishmentQuantity";

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
      && positive(l.itemId) && positive(l.inputUomId)
      && replenishmentBaseQuantity(l.inputQty, l.factorToBase) === l.requestedBaseQty && Number(l.requestedBaseQty) > 0);
}
export function ReplenishmentCreateFields({ values, onChange }: { values: Values; onChange: (v: Values) => void }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const [source, setSource] = useState(values.shortageSource === "material-requests" ? "material-requests" : "low-stock"), [page, setPage] = useState(1), [revision, setRevision] = useState(0);
  const sourceRequestId = positive(values.sourceRequestId) ? String(values.sourceRequestId) : "";
  const sourceItemId = positive(values.sourceItemId) ? String(values.sourceItemId) : "";
  const prefilled = useRef("");
  const branch = String(values.branchId ?? ""), warehouse = String(values.warehouseId ?? "");
  const key = `${branch}:${warehouse}:${source}:${sourceRequestId}:${sourceItemId}:${page}:${revision}`;
  const [loaded, setLoaded] = useState<{ owner: typeof owner; key: string; data?: Page }>();
  useEffect(() => {
    setLoaded(undefined);
    if (owner === null || !positive(branch) || !positive(warehouse)) return;
    const abort = new AbortController();
    void http({ method: "GET", url: `/api/replenishment-shortages/${source}?branchId=${branch}&warehouseId=${warehouse}&page=${page}&pageSize=20${source === "material-requests" && sourceRequestId ? `&sourceRequestId=${encodeURIComponent(sourceRequestId)}` : ""}${source === "low-stock" && sourceItemId ? `&itemId=${encodeURIComponent(sourceItemId)}` : ""}`,
      expectedStatus: 200, decode, signal: abort.signal }).then(result => {
      if (abort.signal.aborted) return;
      setLoaded({ owner, key, data: result.ok ? result.data : undefined });
      if (!result.ok && (result.error.kind === "stale" || result.error.kind === "http" && result.error.status === 401)) void actions.revalidate();
    });
    return () => abort.abort();
  }, [owner, branch, warehouse, source, sourceRequestId, sourceItemId, page, key, actions]);
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
      itemName: c.itemName, unitName: c.unitName,
      units: c.units, inputUomId: c.baseUomId, inputQty: quantity, factorToBase: 1,
    }] });
  }
  useEffect(() => {
    // URL values select a candidate only. Quantity, unit and threshold are
    // prefilled exclusively from the currently authorized server shortage.
    if (source !== "low-stock" || !sourceItemId || !current?.data || prefilled.current === key) return;
    prefilled.current = key;
    const candidate = current.data.items.find(c => String(c.itemId) === sourceItemId);
    if (candidate && lines.length === 0) select(candidate);
  }, [current, key, source, sourceItemId, lines.length]);
  return <section aria-label="Create replenishment from shortages">
    <label>Branch ID<input required type="number" min="1" step="1" value={branch} onChange={e => reset({ ...values, branchId: e.target.value })} /></label>
    <label>Receiving warehouse ID<input required type="number" min="1" step="1" value={warehouse} onChange={e => reset({ ...values, warehouseId: e.target.value })} /></label>
    <label>Supplier (optional)<input maxLength={200} value={String(values.supplierName ?? "")} onChange={e => onChange({ ...values, supplierName: e.target.value })} /></label>
    <label>Expected date (optional)<input type="date" value={String(values.expectedOn ?? "")} onChange={e => onChange({ ...values, expectedOn: e.target.value })} /></label>
    <label>Shortage source<select value={source} onChange={e => { setSource(e.target.value); setPage(1); }}>
      <option value="low-stock">Below minimum stock</option><option value="material-requests">Approved material requests</option>
    </select></label>
    <p>Material demand includes only requests you can currently read. Quantities use base units; these figures do not reserve stock.</p>
    {source === "material-requests" && sourceRequestId && <p>Showing authorized shortages for request {sourceRequestId}.
      <button type="button" onClick={() => reset({ ...values, sourceRequestId: "" })}>Show all visible requests</button></p>}
    <button type="button" onClick={() => { reset(values); setRevision(r => r + 1); }}>Refresh shortages and clear selection</button>
    {!current?.data && <p role="status">{current ? "Shortages unavailable. Check your current grants and warehouse scope." : "Enter branch and warehouse to load authorized shortages."}</p>}
    {current?.data?.items.length === 0 && <p>No visible shortages.</p>}
    {current?.data?.items.map((c, index) => <div key={index}>
      {String(c.itemName || `Item ${c.itemId}`)}, base unit {String(c.unitName || c.baseUomId)}{c.requestNo != null && `, request ${String(c.requestNo)}`}:
      {" "}{String(source === "low-stock" ? c.deficitToMinimum : c.remainingBaseQty)}
      <button type="button" disabled={c.existingActiveReplenishmentId != null} onClick={() => select(c)}>
        {c.existingActiveReplenishmentId != null ? "Already covered" : "Select item"}</button>
      {positive(c.existingActiveReplenishmentId) && <Link to={`/operations/replenishments/${encodeURIComponent(String(c.existingActiveReplenishmentId))}`}>
        Open existing replenishment
      </Link>}
    </div>)}
    <button type="button" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</button>
    <button type="button" disabled={!current?.data || page * 20 >= current.data.total} onClick={() => setPage(p => p + 1)}>Next</button>
    {lines.map(l => <div key={String(l.itemId)}>
      <label>{String(l.itemName || `Item ${l.itemId}`)} unit
        <select value={String(l.inputUomId)} onChange={e => {
          const unit = (Array.isArray(l.units) ? l.units.filter(isObject) : []).find(u => String(u.uomId) === e.target.value);
          if (!unit) return;
          onChange({ ...values, lines: lines.map(x => x.itemId === l.itemId ? { ...x, inputUomId: unit.uomId,
            factorToBase: unit.factorToBase, inputQty: "", requestedBaseQty: null } : x) });
        }}>
          {(Array.isArray(l.units) ? l.units.filter(isObject) : []).map(u => <option key={String(u.uomId)} value={String(u.uomId)}>{String(u.unitName)}</option>)}
        </select>
      </label>
      <label>Requested quantity<input type="number" required min="0.001" step="0.001" value={String(l.inputQty)} onChange={e => onChange({ ...values,
        lines: lines.map(x => x.itemId === l.itemId ? { ...x, inputQty: e.target.value,
          requestedBaseQty: replenishmentBaseQuantity(e.target.value, x.factorToBase) } : x) })} /></label>
      <p>{l.requestedBaseQty == null ? "Enter a positive quantity converting exactly to at most three decimal places in base units." : `Equivalent: ${l.requestedBaseQty} ${l.unitName}.`}</p>
      <button type="button" onClick={() => onChange({ ...values, lines: lines.filter(x => x.itemId !== l.itemId) })}>Remove</button>
    </div>)}
    <p>The server rechecks shortage, source access and duplicate coverage atomically when creating the request.</p>
  </section>;
}
