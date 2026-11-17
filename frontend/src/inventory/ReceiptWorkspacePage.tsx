import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

import { ReceiptPostPanel } from "./ReceiptPostPanel";

type Lookup = { id: number; code: string; name: string; isActive: boolean };
type Line = { itemId: number; uomId: number; expectedQty: string };
type Document = { documentId: number; documentNo: string; destinationWarehouseId: number; documentDate: string; status: string; rowVersion: string; note: string | null;
  lines: { itemId: number; uomId: number; requestedQty: number }[] };
type Snapshot = { warehouses: Lookup[]; items: { id: number; code: string; name: string; baseUnitId: number }[]; units: Lookup[];
  conversions: { itemId: number; unitId: number }[]; documents: { items: Document[]; totalCount: number } };
type Draft = { documentId: number; warehouseId: number; date: string; sourceLabel: string; referenceNumber: string; replenishmentId: string; note: string; version: string; lines: Line[] };

export function ReceiptWorkspacePage() {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const [page, setPage] = useState(1), [revision, setRevision] = useState(0);
  const [snapshot, setSnapshot] = useState<{ owner: typeof owner; page: number; value: Snapshot } | null>(null);
  const [postId, setPostId] = useState<number | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null), [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false);
  const writer = useRef<AbortController | null>(null);
  const data = snapshot?.owner === owner && snapshot.page === page ? snapshot.value : null;
  useEffect(() => { writer.current?.abort(); setPostId(null); setDraft(null); setSnapshot(null); setMessage(""); setBusy(false); }, [owner, page]);
  useEffect(() => {
    setSnapshot(null); if (!owner) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/receipt-workspace?page=${page}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        if (response.status === 401) actions.invalidateSession(owner);
        if (response.status === 403) setPostId(null);
        if (!response.ok) throw new Error((await response.json()).detail ?? `Receipt workspace unavailable (${response.status}).`);
        const value = await response.json() as Snapshot;
        if (!Array.isArray(value.documents?.items) || !Array.isArray(value.warehouses)) throw new Error("Invalid receipt response.");
        if (!abort.signal.aborted && current.current === owner) setSnapshot({ owner, page, value });
      } catch (e) { if (!abort.signal.aborted && current.current === owner) { setDraft(null); setMessage(e instanceof Error ? e.message : "Receipt workspace unavailable."); } }
    })();
    const refresh = () => { setSnapshot(null); setRevision(v => v + 1); };
    window.addEventListener("focus", refresh); const timer = window.setInterval(refresh, 30000);
    return () => { abort.abort(); clearInterval(timer); window.removeEventListener("focus", refresh); };
  }, [owner, page, revision, actions]);
  useEffect(() => () => writer.current?.abort(), []);
  async function edit(document: Document) {
    if (!owner || busy) return;
    const abort = new AbortController(); writer.current = abort; setBusy(true); setMessage("");
    try {
      const response = await fetch(`/api/receipt-workspace/${document.documentId}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      const result = await response.json();
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) throw new Error(result.detail ?? `Receipt unavailable (${response.status}).`);
      if (abort.signal.aborted || current.current !== owner) return;
      const d = result.document as Document;
      if (d.status !== "DRAFT") throw new Error("Receipt is no longer Draft. Reload the list.");
      setDraft({ documentId: d.documentId, warehouseId: d.destinationWarehouseId, date: d.documentDate, version: d.rowVersion, note: d.note ?? "",
        sourceLabel: result.source?.sourceLabel ?? "", referenceNumber: result.source?.referenceNumber ?? "", replenishmentId: String(result.source?.replenishmentId ?? ""),
        lines: d.lines.map((l, index) => ({ itemId: l.itemId, uomId: l.uomId, expectedQty: result.expectedQuantities[index] })) });
    } catch (e) { if (!abort.signal.aborted) { setDraft(null); setMessage(e instanceof Error ? e.message : "Read failed."); } }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  async function mutate(cancel?: Document) {
    if (!owner || !data || busy || uncertain || (!cancel && !draft)) return;
    const reason = cancel ? window.prompt("Cancellation reason (at least 10 characters)") : null;
    if (cancel && (!reason || reason.trim().length < 10 || !window.confirm("Cancel this Draft receipt? No stock will change."))) return;
    const selected = draft;
    const abort = new AbortController(); writer.current = abort; setBusy(true); setMessage(""); let sent = false;
    try {
      const response = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      const csrf = await response.json();
      if (!response.ok || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string") throw new Error("CSRF unavailable.");
      if (current.current !== owner || abort.signal.aborted) return;
      const id = cancel?.documentId ?? selected!.documentId;
      sent = true;
      const result = await fetch(cancel ? `/api/inventory/documents/${id}/cancel` : `/api/inventory/receipts${id ? `/${id}` : ""}`, {
        method: cancel || !id ? "POST" : "PUT", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken },
        body: JSON.stringify(cancel ? { expectedRowVersion: cancel.rowVersion, reason } : { destinationWarehouseId: selected!.warehouseId, sourceWarehouseId: null,
          documentDate: selected!.date, sourceLabel: selected!.sourceLabel, referenceNumber: selected!.referenceNumber || null,
          replenishmentId: selected!.replenishmentId ? Number(selected!.replenishmentId) : null, note: selected!.note || null,
          expectedRowVersion: selected!.version || null, lines: selected!.lines }) });
      if (result.status === 401) actions.invalidateSession(owner);
      if (result.status === 403) { setSnapshot(null); setDraft(null); }
      if (!result.ok) {
        if (result.status < 500) sent = false;
        throw new Error((await result.json()).detail ?? `Receipt operation failed (${result.status}).`);
      }
      if (current.current !== owner || abort.signal.aborted) { setUncertain(true); return; }
      setDraft(null); setMessage(cancel ? "MSG08: Draft cancelled. Stock unchanged." : "MSG08: Draft saved. Stock unchanged."); setRevision(v => v + 1);
    } catch (e) { if (sent) setUncertain(true); if (!abort.signal.aborted) setMessage(`${e instanceof Error ? e.message : "Save failed."}${sent ? " Outcome unknown: inspect the list before attempting a new operation." : ""}`); }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  const field = (patch: Partial<Draft>) => { if (draft) setDraft({ ...draft, ...patch }); };
  return <section><h1>Stock receipts</h1><Link to="/inventory/stock">Stock balance</Link> | <Link to="/operations/replenishments">Replenishments</Link>
    {message && <p role="status">{message}</p>}
    <button disabled={busy} onClick={() => { setDraft(null); setRevision(v => v + 1); }}>Reload receipts</button>
    {uncertain && <button disabled={busy} onClick={() => { if (window.confirm("Have you inspected current receipts and confirmed the previous outcome?")) { setDraft(null); setUncertain(false); } }}>Confirm outcome reviewed</button>}
    {postId != null && <ReceiptPostPanel key={postId} id={postId} items={data?.items ?? []} units={data?.units ?? []} onSaved={() => setRevision(v => v + 1)} />}
    {data && <><button disabled={busy || uncertain} onClick={() => setDraft({ documentId: 0, warehouseId: 0, date: "", sourceLabel: "", referenceNumber: "", replenishmentId: "", note: "", version: "", lines: [{ itemId: 0, uomId: 0, expectedQty: "" }] })}>Create receipt</button>
      <table><thead><tr><th>Receipt</th><th>Date</th><th>Warehouse</th><th>Status</th><th>Actions</th></tr></thead><tbody>{data.documents.items.map(d => <tr key={d.documentId}>
        <td><Link to={`/inventory/documents/${d.documentId}`}>{d.documentNo}</Link></td><td>{d.documentDate}</td><td>{data.warehouses.find(w => w.id === d.destinationWarehouseId)?.name}</td><td>{d.status}</td>
        <td><button disabled={busy} onClick={() => { setDraft(null); setPostId(d.documentId); }}>Verify / posting status</button>{d.status === "DRAFT" && <><button disabled={busy || uncertain} onClick={() => void edit(d)}>Edit</button><button disabled={busy || uncertain} onClick={() => void mutate(d)}>Cancel</button></>}</td></tr>)}</tbody></table>
      <button disabled={busy || !!draft || page === 1} onClick={() => setPage(p => p - 1)}>Previous</button><button disabled={busy || !!draft || page * 25 >= data.documents.totalCount} onClick={() => setPage(p => p + 1)}>Next</button>
      {draft && <form onSubmit={e => { e.preventDefault(); void mutate(); }}><fieldset disabled={busy || uncertain}>
        <label>Receiving warehouse <select required value={draft.warehouseId || ""} onChange={e => field({ warehouseId: Number(e.target.value), replenishmentId: "" })}><option value="">Select warehouse</option>{data.warehouses.filter(w => w.isActive).map(w => <option key={w.id} value={w.id}>{w.name}</option>)}</select></label>
        <label>Document date <input required type="date" value={draft.date} onChange={e => field({ date: e.target.value })} /></label>
        <label>Supplier / internal source <input required maxLength={200} value={draft.sourceLabel} onChange={e => field({ sourceLabel: e.target.value })} /></label>
        <label>Reference document number <input maxLength={100} value={draft.referenceNumber} onChange={e => field({ referenceNumber: e.target.value })} /></label>
        <label>Linked replenishment ID (optional) <input type="number" min={1} step={1} value={draft.replenishmentId} onChange={e => field({ replenishmentId: e.target.value })} /></label>
        <p>Linked replenishments require separate read permission, the same warehouse and matching items. Posting a linked receipt is coordinated from its replenishment.</p>
        {draft.lines.map((l, index) => <fieldset key={index}><legend>Line {index + 1}</legend>
          <select required aria-label={`Item ${index + 1}`} value={l.itemId || ""} onChange={e => field({ lines: draft.lines.map((v, i) => i !== index ? v : { ...v, itemId: Number(e.target.value), uomId: data.items.find(x => x.id === Number(e.target.value))?.baseUnitId ?? 0 }) })}><option value="">Select item</option>{data.items.map(i => <option key={i.id} value={i.id}>{i.code} - {i.name}</option>)}</select>
          <select required aria-label={`Unit ${index + 1}`} value={l.uomId || ""} onChange={e => field({ lines: draft.lines.map((v, i) => i !== index ? v : { ...v, uomId: Number(e.target.value) }) })}><option value="">Select unit</option>{data.units.filter(u => u.id === data.items.find(i => i.id === l.itemId)?.baseUnitId || data.conversions.some(c => c.itemId === l.itemId && c.unitId === u.id)).map(u => <option key={u.id} value={u.id}>{u.name}</option>)}</select>
          <input required aria-label={`Expected quantity ${index + 1}`} inputMode="decimal" pattern="[0-9]+([.][0-9]{1,3})?" value={l.expectedQty} onChange={e => field({ lines: draft.lines.map((v, i) => i !== index ? v : { ...v, expectedQty: e.target.value }) })} />
          <button type="button" disabled={draft.lines.length === 1} onClick={() => field({ lines: draft.lines.filter((_, i) => i !== index) })}>Remove line</button>
        </fieldset>)}
        <button type="button" onClick={() => field({ lines: [...draft.lines, { itemId: 0, uomId: 0, expectedQty: "" }] })}>Add line</button>
        <label>Note <textarea value={draft.note} onChange={e => field({ note: e.target.value })} /></label><button>Save Draft</button>
      </fieldset></form>}
    </>}
  </section>;
}
