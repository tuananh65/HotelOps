import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

type Unit = { uomId: number; conversionFactor: number };
type Line = { stockDocumentLineId: number; itemId: number; documentNo: string; lineNo: number;
  baseUomId: number; allowedUoms: Unit[]; remainingBaseQty: number };
type Selection = { line: Line; uomId: number; quantity: string };
const positiveId = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
function validLine(v: unknown): v is Line {
  return isObject(v) && positiveId(v.stockDocumentLineId) && positiveId(v.itemId) && positiveId(v.lineNo)
    && positiveId(v.baseUomId) && typeof v.documentNo === "string" && Array.isArray(v.allowedUoms)
    && v.allowedUoms.length > 0 && v.allowedUoms.every(u => isObject(u) && positiveId(u.uomId)
      && typeof u.conversionFactor === "number" && Number.isFinite(u.conversionFactor) && u.conversionFactor > 0)
    && typeof v.remainingBaseQty === "number" && Number.isFinite(v.remainingBaseQty) && v.remainingBaseQty >= 0;
}
function validSelection(s: Selection) {
  const factor = s.line.allowedUoms.find(u => u.uomId === s.uomId)?.conversionFactor;
  const quantity = Number(s.quantity);
  return /^\d+(?:\.\d{1,3})?$/.test(s.quantity) && Number.isFinite(quantity) && quantity > 0
    && factor !== undefined && quantity * factor <= s.line.remainingBaseQty + 1e-9;
}

export function MaintenanceProgressPanel({ data, allowMaterials, onCommitted }: { data: Values; allowMaterials: boolean; onCommitted?: (message: string) => void }) {
  const { state } = useAuth(), session = state.session;
  const [bound] = useState(session), current = useRef(session); current.current = session;
  const [percent, setPercent] = useState("");
  const [note, setNote] = useState(""), [selected, setSelected] = useState<Selection[]>([]);
  const [lines, setLines] = useState<Line[]>([]), [page, setPage] = useState(0), [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false), [materialMessage, setMaterialMessage] = useState("");
  const [busy, setBusy] = useState(false), [attempted, setAttempted] = useState(false), [review, setReview] = useState(false);
  const [message, setMessage] = useState("");
  const reader = useRef<AbortController | null>(null), writer = useRef<AbortController | null>(null), sending = useRef(false);
  useEffect(() => () => { reader.current?.abort(); writer.current?.abort(); }, [session, data]);
  const id = data.maintenanceWorkId, tag = data.executionETag, actor = Number(session?.user.id);
  if (!session || session !== bound || data.canLog !== true || !["IN_PROGRESS", "ON_HOLD"].includes(String(data.status))
    || !positiveId(id) || !positiveId(actor) || typeof tag !== "string" || !/^"[0-9A-F]{16}"$/.test(tag)) return null;
  const valid = (data.progressPercentAvailable !== true || (/^\d{1,3}$/.test(percent) && Number(percent) <= 100)) && note.trim().length > 0 && selected.every(validSelection) && (selected.length === 0 || allowMaterials);
  async function load(next: number) {
    if (!allowMaterials || sending.current || attempted || current.current !== session) return;
    reader.current?.abort(); const abort = new AbortController(); reader.current = abort;
    setLoading(true); setMaterialMessage("");
    try {
      const response = await fetch(`/api/maintenance-works/${id}/material-availability?page=${next}&pageSize=20`,
        { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!response.ok) throw Error("Material availability is unavailable. Current inventory and material request access are required.");
      const result: unknown = await response.json();
      if (!isObject(result) || result.page !== next || result.pageSize !== 20 || !Number.isSafeInteger(result.total)
        || typeof result.total !== "number" || result.total < 0 || !Array.isArray(result.items)
        || result.items.length > 20 || !result.items.every(validLine)
        || new Set(result.items.map(x => x.stockDocumentLineId)).size !== result.items.length)
        throw Error("Material availability could not be confirmed.");
      if (!abort.signal.aborted && current.current === session) {
        setLines(result.items); setPage(next); setTotal(result.total);
        const refreshed = result.items as Line[];
        setSelected(items => items.map(item => {
          const line = refreshed.find(row => row.stockDocumentLineId === item.line.stockDocumentLineId);
          return line ? { ...item, line } : item;
        }));
      }
    } catch (error) {
      if (!abort.signal.aborted && current.current === session) {
        setLines([]); setPage(0); setTotal(0);
        setMaterialMessage(error instanceof Error ? error.message : "Material availability unavailable.");
      }
    } finally { if (!abort.signal.aborted) setLoading(false); }
  }
  async function save() {
    if (!valid || loading || sending.current || attempted || current.current !== session) return;
    sending.current = true; setBusy(true); setAttempted(true); setMessage("");
    const abort = new AbortController(); writer.current = abort;
    let dispatched = false;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!bootstrap.ok) throw Error("Session check failed. Reload before retrying.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string"
        || !/^[\x21-\x7e]+$/.test(csrf.csrfToken) || abort.signal.aborted || current.current !== session)
        throw Error("Session changed. Reload before retrying.");
      dispatched = true;
      const response = await fetch(`/api/maintenance-works/${id}/logs`, {
        method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": tag as string },
        body: JSON.stringify({ actorUserId: actor, ...(data.progressPercentAvailable === true ? { progressPercent: Number(percent) } : {}), workNote: note.trim(), spentMinutes: 0,
          materialUsages: selected.map(s => ({ stockDocumentLineId: s.line.stockDocumentLineId, uomId: s.uomId, usedQty: Number(s.quantity) })) })
      });
      if (!response.ok) {
        if ([400, 409, 422].includes(response.status)) {
          // The server rejected the command. Permit corrections, but never retry
          // an ambiguous transport/server failure or reuse a stale ETag.
          if (!abort.signal.aborted && current.current === session) setAttempted(false);
          throw Error("Progress was rejected. Check notes, units and quantities. Load issued materials again to refresh availability, correct the entries and review before saving.");
        }
        throw Error(response.status === 412 ? "Work changed. Reload before recording progress again."
          : "Progress was not confirmed. Reload and check current access, source documents and remaining quantities.");
      }
      const result: unknown = await response.json();
      if (!isObject(result) || result.maintenanceWorkId !== id || result.loggedBy !== actor || !positiveId(result.logId)
        || (data.progressPercentAvailable === true && result.progressPercent !== Number(percent))
        || result.workNote !== note.trim() || result.status !== data.status || typeof result.executionETag !== "string"
        || !/^"[0-9A-F]{16}"$/.test(result.executionETag) || result.executionETag === tag
        || !Array.isArray(result.materialUsages) || result.materialUsages.length !== selected.length
        || !selected.every(s => (result.materialUsages as unknown[]).filter(u => isObject(u)
          && u.stockDocumentLineId === s.line.stockDocumentLineId && u.uomId === s.uomId
          && u.usedQty === Number(s.quantity) && u.maintenanceWorkId === id && u.maintenanceWorkLogId === result.logId).length === 1))
        throw Error("Progress response could not be confirmed. Reload to check current records.");
      if (!abort.signal.aborted && current.current === session) {
        const notice = "Progress recorded. Reaching 100% does not submit or close the work.";
        setMessage(notice);
        onCommitted?.(notice);
      }
    } catch (error) {
      if (!abort.signal.aborted && current.current === session) {
        if (!dispatched) setAttempted(false);
        setMessage(error instanceof Error ? error.message : "Progress not confirmed. Reload.");
      }
    } finally { if (!abort.signal.aborted) { setBusy(false); setReview(false); sending.current = false; } }
  }
  return <section aria-label="Maintenance progress">
    <h3>Record progress and materials</h3>
    <p>Actual effort comes from work intervals. Materials must come from eligible posted issue lines for this incident.</p>
    {message && <p role="status">{message}</p>}
    <fieldset disabled={busy || attempted || review}>
      {data.progressPercentAvailable === true && <label>Progress (%)<input type="number" min="0" max="100" step="1" value={percent} onChange={e => setPercent(e.target.value)} /></label>}
      {data.progressPercentAvailable !== true && <p>Percentage tracking is unavailable. You can still record notes.</p>}
      <p>Reaching 100% does not submit or close the work.</p>
      <label>Progress note<textarea value={note} onChange={e => setNote(e.target.value)} /></label>
      <p>Include your diagnosis and actions taken in the progress note.</p>
      {allowMaterials ? <>
        <button disabled={loading} onClick={() => void load(1)}>Load issued materials</button>
        {materialMessage && <p role="status">{materialMessage}</p>}
        {loading && <p>Loading eligible issue lines…</p>}
        {page > 0 && <><p>{total} eligible lines. Page {page}.</p>
          {lines.length === 0 && <p>No eligible issued materials in this page.</p>}
          {lines.map(line => <p key={line.stockDocumentLineId}>{line.documentNo}, line {line.lineNo}, item {line.itemId}: {line.remainingBaseQty} remaining (base unit {line.baseUomId}).
            <button disabled={loading || line.remainingBaseQty <= 0 || selected.some(s => s.line.stockDocumentLineId === line.stockDocumentLineId)}
              onClick={() => setSelected(items => [...items, { line, uomId: line.allowedUoms[0].uomId, quantity: "" }])}>Add line {line.lineNo} from {line.documentNo}</button></p>)}
          <button disabled={loading || page <= 1} onClick={() => void load(page - 1)}>Previous materials</button>
          <button disabled={loading || page * 20 >= total} onClick={() => void load(page + 1)}>Next materials</button>
        </>}
      </> : <p>Material selection is unavailable. You can record a progress note.</p>}
      {selected.map(s => <div key={s.line.stockDocumentLineId}>
        <p>{s.line.documentNo}, line {s.line.lineNo}, item {s.line.itemId}</p>
        <label>Unit for line {s.line.stockDocumentLineId}<select aria-label={`Unit for line ${s.line.stockDocumentLineId}`} value={s.uomId} onChange={e => setSelected(items => items.map(x => x === s ? { ...x, uomId: Number(e.target.value) } : x))}>
          {s.line.allowedUoms.map(u => <option key={u.uomId} value={u.uomId}>Unit {u.uomId} ({u.conversionFactor} base units)</option>)}
        </select></label>
        <label>Used quantity for line {s.line.stockDocumentLineId}<input inputMode="decimal" value={s.quantity} onChange={e => setSelected(items => items.map(x => x === s ? { ...x, quantity: e.target.value } : x))} /></label>
        {!validSelection(s) && <p>Enter a positive quantity with at most three decimal places within the remaining base quantity ({s.line.remainingBaseQty}).</p>}
        <button onClick={() => setSelected(items => items.filter(x => x !== s))}>Remove line {s.line.stockDocumentLineId}</button>
      </div>)}
    </fieldset>
    {review ? <><p>Record this progress note and {selected.length} material usage line(s)?</p><p>{note.trim()}</p>
      <button disabled={busy} onClick={() => void save()}>Confirm progress</button><button disabled={busy} onClick={() => setReview(false)}>Cancel</button></>
      : <button disabled={!valid || busy || attempted || loading} onClick={() => setReview(true)}>Review progress</button>}
  </section>;
}
