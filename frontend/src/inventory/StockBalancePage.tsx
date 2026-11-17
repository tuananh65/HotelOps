import { useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";

type Option = { id: number; name: string; branchId?: number; canReplenish?: boolean; canConfigureMinimum?: boolean };
type Row = { warehouseId: number; itemId: number; item: string; unit: string; available: number;
  outboundInTransit: number; inboundInTransit: number; minimum: number | null; belowMinimum: boolean };
type Movement = { movementId: number; documentId: number; document: string; type: string; at: string; quantity: number; runningBalance: number };
type Result = { snapshot: string; warehouses: Option[]; categories: Option[]; items: Row[]; totalCount: number;
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
          throw new Error(response.status === 403 ? "ACCESS_DENIED: current inventory permission and branch scope required." : body?.detail ?? body?.message ?? `Stock read failed (${response.status}).`);
        }
        const body = await response.json() as Result;
        if (!Array.isArray(body.items) || !Array.isArray(body.ledger) || !Array.isArray(body.warehouses) || !Array.isArray(body.categories)
          || !Number.isSafeInteger(body.totalCount) || body.totalCount < 0 || body.filters?.page !== Number(params.get("page") ?? 1)
          || body.filters.warehouseId !== (params.get("warehouseId") ? Number(params.get("warehouseId")) : null)
          || body.filters.itemId !== (params.get("itemId") ? Number(params.get("itemId")) : null)) throw new Error("Unexpected stock response; reload.");
        if (!controller.signal.aborted) setResult({ owner, query, data: body });
      } catch (e) { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : "Stock unavailable."); }
    })();
    const recheck = () => { setResult(null); setRefresh(v => v + 1); };
    window.addEventListener("focus", recheck);
    const timer = window.setInterval(recheck, 30000);
    return () => { controller.abort(); window.removeEventListener("focus", recheck); window.clearInterval(timer); };
  }, [owner, query, refresh, actions, params]);
  const change = (name: string, value: string) => {
    const next = new URLSearchParams(params); value ? next.set(name, value) : next.delete(name);
    if (name !== "page") next.delete("page");
    setParams(next);
  };
  const ledger = (row: Row) => {
    const next = new URLSearchParams(params); next.set("warehouseId", String(row.warehouseId)); next.set("itemId", String(row.itemId)); next.delete("page"); setParams(next);
  };
  return <section aria-label="Stock balance and movement ledger">
    <h1>Stock balance and movement ledger</h1>
    <p>Quantities use each item's base unit. Inbound and outbound in-transit quantities are separate; they are not available stock.</p>
    <Link to="/inventory/items">Item catalogue</Link>
    {!owner ? <p role="status">Confirm your session to view stock.</p> : <>
      <label>Warehouse <select value={params.get("warehouseId") ?? ""} onChange={e => change("warehouseId", e.target.value)}>
        <option value="">All authorized warehouses</option>{data?.warehouses.map(w => <option key={w.id} value={w.id}>{w.name}</option>)}
      </select></label>
      <label>Item ID <input type="number" min="1" value={params.get("itemId") ?? ""} onChange={e => change("itemId", e.target.value)} /></label>
      <label>Type/category <select value={params.get("categoryId") ?? ""} onChange={e => change("categoryId", e.target.value)}>
        <option value="">All types</option>{data?.categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
      </select></label>
      <label><input type="checkbox" checked={params.get("belowMinimum") === "true"} onChange={e => change("belowMinimum", e.target.checked ? "true" : "")} />Below minimum</label>
      <label>From (UTC) <input type="date" value={params.get("from") ?? data?.filters.from ?? ""} onChange={e => change("from", e.target.value)} /></label>
      <label>To (UTC) <input type="date" value={params.get("to") ?? data?.filters.to ?? ""} onChange={e => change("to", e.target.value)} /></label>
      <button onClick={() => { setResult(null); setRefresh(v => v + 1); }}>Reload</button>
      {error && <p role="alert">{error}</p>}
      {!data && !error && <p role="status">Loading current authorized stock…</p>}
      {data && <>
        {data.warehouses.some(w => w.canConfigureMinimum) && <Link to={`/inventory/minimums${data.filters.warehouseId ? `?warehouseId=${data.filters.warehouseId}` : ""}`}>Configure minimum stock levels</Link>}
        <p>Snapshot {data.snapshot}; {data.totalCount} matching stock pairs.</p>
        {data.items.length === 0 && <p>MSG10: No stock pairs match the applied filters.</p>}
        <table><thead><tr><th>Warehouse</th><th>Item / unit</th><th>Available</th><th>Outbound transit</th><th>Inbound transit</th><th>Minimum</th><th>Ledger</th></tr></thead>
          <tbody>{data.items.map(r => <tr key={`${r.warehouseId}:${r.itemId}`}>
            <td>{data.warehouses.find(w => w.id === r.warehouseId)?.name}</td><td>{r.item} / {r.unit}</td>
            <td>{r.available}{r.belowMinimum && <strong> — Below minimum</strong>}</td><td>{r.outboundInTransit}</td><td>{r.inboundInTransit}</td><td>{r.minimum ?? "Not configured"}</td>
            <td><button onClick={() => ledger(r)}>View ledger</button>
              {r.belowMinimum && data.warehouses.find(w => w.id === r.warehouseId)?.canReplenish === true &&
                <Link to={`/operations/replenishments?action=A004A-056&branchId=${data.warehouses.find(w => w.id === r.warehouseId)?.branchId}&warehouseId=${r.warehouseId}&itemId=${r.itemId}&shortageSource=low-stock`}>Create replenishment</Link>}
            </td></tr>)}</tbody></table>
        <button disabled={data.filters.page <= 1} onClick={() => change("page", String(data.filters.page - 1))}>Previous</button>
        <button disabled={data.filters.page * data.filters.pageSize >= data.totalCount} onClick={() => change("page", String(data.filters.page + 1))}>Next</button>
        {data.openingBalance !== null && <>
          <h2>Posted movement ledger</h2><p>{data.filters.from} – {data.filters.to} UTC. Opening {data.openingBalance}; closing {data.closingBalance}.</p>
          {data.ledger.length === 0 && <p>MSG10: No movements in this period. Opening balance is retained.</p>}
          <table><thead><tr><th>At (UTC)</th><th>Document</th><th>Type</th><th>Quantity</th><th>Running balance</th></tr></thead>
            <tbody>{data.ledger.map(m => <tr key={m.movementId}><td>{m.at}</td><td><Link to={`/inventory/documents/${m.documentId}`}>{m.document}</Link></td>
              <td>{m.type}</td><td>{m.quantity}</td><td>{m.runningBalance}</td></tr>)}</tbody></table>
          <p>Opening a document or reversing it requires the document's current permissions independently.</p>
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
        if (!response.ok) throw new Error(`Document unavailable (${response.status}); current access to both warehouses is required for a transfer.`);
        const body = await response.json() as Document;
        if (String(body.documentId) !== id || !Array.isArray(body.lines) || typeof body.rowVersion !== "string") throw new Error("Unexpected document response.");
        if (!controller.signal.aborted) setLoaded({ owner, id, data: body });
      } catch (e) { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : "Document unavailable."); }
    })();
    const recheck = () => { setLoaded(null); setRevision(v => v + 1); };
    window.addEventListener("focus", recheck); const timer = window.setInterval(recheck, 30000);
    return () => { controller.abort(); window.removeEventListener("focus", recheck); window.clearInterval(timer); };
  }, [owner, id, actions, revision]);
  return <section><h1>Stock document</h1><Link to="/inventory/stock">Stock balance and ledger</Link>
    {error && <p role="alert">{error}</p>}
    {data && <><h2>{data.documentNo}</h2><p>{data.documentType} — {data.status}; document date {data.documentDate}; posted {data.postedAt ?? "Not posted"}.</p>
      <p>Source warehouse {data.sourceWarehouseId ?? "N/A"}; destination {data.destinationWarehouseId ?? "N/A"}.</p>
      <p>Selected document ID: {data.documentId}. Reversal is a separate UC088 operation requiring current reversal authority.</p>
      <table><thead><tr><th>Item</th><th>Unit</th><th>Actual</th><th>Base quantity</th><th>Received</th><th>Damaged</th><th>Missing</th><th>In transit</th></tr></thead>
        <tbody>{data.lines.map(l => <tr key={l.lineId}><td>{l.itemId}</td><td>{l.uomId}</td><td>{l.actualQty}</td><td>{l.baseQty}</td><td>{l.receivedQty ?? "N/A"}</td><td>{l.damagedQty ?? "N/A"}</td><td>{l.missingQty ?? "N/A"}</td><td>{l.inTransitQty ?? "N/A"}</td></tr>)}</tbody></table>
    </>}
  </section>;
}
