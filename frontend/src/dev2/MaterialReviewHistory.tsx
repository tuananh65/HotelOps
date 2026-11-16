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
  return <section aria-label="Recent material requests for review">
    <h3>Current stock preview</h3>
    {currentStock?.data ? <MaterialAvailabilityView data={currentStock.data} />
      : <p role="status">{currentStock ? "Stock unavailable. Independent inventory permission and current review authority are required; no stock has been reserved." : "Loading authorized stock preview…"}</p>}
    <h3>Recent requests for the same material</h3>
    <p>Up to five newest other requests by this requester in this branch, within your current MR_VIEW scope. Historical quantities do not reserve stock.</p>
    <label>Material<select value={itemId} onChange={e => setSelected(e.target.value)}>
      {items.map(l => <option key={String(l.itemId)} value={String(l.itemId)}>{String(l.itemName ?? l.itemId)}</option>)}
    </select></label>
    <button type="button" onClick={() => setRevision(r => r + 1)}>Refresh review context</button>
    {!current ? <p role="status">Loading recent requests…</p> : !current.page ? <p role="alert">Recent requests unavailable. Check your current read access and retry.</p>
      : rows.length === 0 ? <p>No other matching requests visible.</p> : <table>
        <thead><tr><th>Request</th><th>Created</th><th>Status</th><th>Quantities by unit</th></tr></thead>
        <tbody>{rows.map(r => <tr key={String(r.requestId)}>
          <td><Link to={`/operations/material-requests/${encodeURIComponent(String(r.requestId))}`}>{String(r.requestNo)}</Link></td>
          <td>{String(r.createdAt)}</td><td>{String(r.status)}</td>
          <td>{(Array.isArray(r.quantities) ? r.quantities.filter(isObject) : []).map(l => <p key={String(l.requestLineId)}>
            {String(l.itemName)}: requested {String(l.requestedQty)}, approved {String(l.approvedQty ?? "Not approved")}, issued {String(l.fulfilledQty)} {String(l.unitName)}
          </p>)}</td>
        </tr>)}</tbody>
      </table>}
    <Link to={`/operations/material-requests?branchId=${encodeURIComponent(branch)}&requesterId=${encodeURIComponent(requester)}&itemId=${encodeURIComponent(itemId)}`}>Open all matching requests and filters</Link>
  </section>;
}
