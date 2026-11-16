import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Occurrence = { runId: number; scheduleId: number; title: string; instruction: string; status: string;
  dueAt: string; performedAt: string | null; result: string | null; note: string | null; assigneeId: number | null;
  assetId: number | null; roomId: number | null; areaId: number | null; version: string; canAssign: boolean; canCancel: boolean; canStart: boolean;
  checkItems: { key: string; description: string; required: boolean }[] };
const positive = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
function valid(v: unknown): v is Occurrence {
  return isObject(v) && positive(v.runId) && positive(v.scheduleId) && typeof v.title === "string" && typeof v.instruction === "string"
    && typeof v.status === "string" && typeof v.dueAt === "string" && Number.isFinite(Date.parse(v.dueAt))
    && (v.performedAt === null || typeof v.performedAt === "string" && Number.isFinite(Date.parse(v.performedAt)))
    && [v.result,v.note].every(x => x === null || typeof x === "string")
    && [v.assigneeId,v.assetId,v.roomId,v.areaId].every(x => x === null || positive(x))
    && typeof v.version === "string" && /^"[0-9A-F]{16}"$/.test(v.version) && typeof v.canAssign === "boolean" && typeof v.canCancel === "boolean" && typeof v.canStart === "boolean"
    && Array.isArray(v.checkItems) && v.checkItems.every(c => isObject(c) && typeof c.key === "string"
      && typeof c.description === "string" && typeof c.required === "boolean");
}
export function PreventiveOccurrencesPanel({ branchId }: { branchId: number }) {
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const mounted = useRef(owner).current, current = useRef(owner); current.current = owner;
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);
  const [rows,setRows] = useState<Occurrence[]>([]), [message,setMessage] = useState("");
  const [busy,setBusy] = useState(false), sending = useRef(false);
  const [assignees,setAssignees] = useState<Record<number,string>>({}), [reasons,setReasons] = useState<Record<number,string>>({});
  const [uncertain,setUncertain] = useState(false);
  async function change(row: Occurrence, cancel: boolean, start = false) {
    if (!owner || owner !== mounted || sending.current || uncertain) return;
    const assigneeId = Number(assignees[row.runId]), reason = reasons[row.runId]?.trim() ?? "";
    if (!start && (cancel ? reason.length < 10 || reason.length > 4000 : !positive(assigneeId))) { setMessage("Choose a technician, or provide a cancellation reason of at least 10 characters."); return; }
    const abort = new AbortController(); controller.current = abort; sending.current = true; setBusy(true); setMessage("");
    let submitted = false;
    try {
      const bootstrap = await fetch("/api/auth/csrf", {credentials:"same-origin",cache:"no-store",redirect:"error",signal:abort.signal});
      if (!bootstrap.ok) throw Error("Session check failed.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !/^[\x21-\x7e]+$/.test(csrf.csrfToken)
        || abort.signal.aborted || current.current !== owner) throw Error("Session changed. Reload.");
      submitted = true;
      const response = await fetch(`/api/branches/${branchId}/preventive-occurrences/${row.runId}/${start ? "start" : cancel ? "cancel" : "assign"}`, {
        method:"POST",credentials:"same-origin",cache:"no-store",redirect:"error",signal:abort.signal,
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":row.version},
        body:JSON.stringify(start ? {} : cancel ? {reason} : {assigneeId}) });
      if (!response.ok) throw Error(response.status === 412 ? "Occurrence changed." : `Change not confirmed (${response.status}).`);
      const raw: unknown = await response.json();
      if (!valid(raw) || raw.runId !== row.runId) throw Error("Change response could not be confirmed.");
      if (!abort.signal.aborted && current.current === owner) { setRows(old => old.map(r => r.runId === raw.runId ? raw : r)); setMessage(start ? "Occurrence started; work interval opened." : cancel ? "Occurrence cancelled." : "Technician assigned."); }
    } catch(e) { if (!abort.signal.aborted && current.current === owner) { setMessage(String(e)+(submitted ? " Reload before another change." : "")); if(submitted)setUncertain(true); } }
    finally { sending.current=false; if(!abort.signal.aborted && current.current===owner)setBusy(false); }
  }
  async function load(more = false) {
    if (!owner || owner !== mounted || sending.current) return;
    const abort = new AbortController(); controller.current = abort;
    sending.current = true; setBusy(true); setMessage("");
    try {
      const response = await fetch(`/api/branches/${branchId}/preventive-occurrences?afterId=${more ? rows.at(-1)?.runId ?? 0 : 0}`,
        { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!response.ok) throw Error(`Occurrences unavailable (${response.status}). Check current access and configuration.`);
      const raw: unknown = await response.json();
      if (!Array.isArray(raw) || !raw.every(valid)) throw Error("Occurrence details could not be confirmed.");
      if (!abort.signal.aborted && current.current === owner) { setRows(more ? [...rows,...raw] : raw); if (!raw.length) setMessage("No further occurrences in your current scope."); }
    } catch (e) { if (!abort.signal.aborted && current.current === owner) { setRows([]); setMessage(String(e)); } }
    finally { sending.current = false; if (!abort.signal.aborted && current.current === owner) setBusy(false); }
  }
  if (!owner || owner !== mounted || !positive(branchId)) return null;
  return <section aria-label="Scheduled maintenance occurrences"><h2>Scheduled maintenance</h2>
    <button disabled={busy} onClick={() => void load()}>Load occurrences</button>
    {!!rows.length && <button disabled={busy} onClick={() => void load(true)}>Load more occurrences</button>}
    {message && <p role="status">{message}</p>}
    {rows.map(r => <details key={r.runId}><summary>{r.title} — {r.status} — {new Date(r.dueAt).toLocaleString()}</summary>
      <p>{r.instruction}</p><p>Occurrence #{r.runId}; schedule #{r.scheduleId}; assignee {r.assigneeId ?? "Pending assignment"}.</p>
      {r.canStart && <button disabled={busy || uncertain} onClick={() => void change(r,false,true)}>Start occurrence</button>}
      <p>{r.roomId ? `Room #${r.roomId}` : r.areaId ? `Area #${r.areaId}` : `Asset #${r.assetId}`}</p>
      <ul>{r.checkItems.map(c => <li key={c.key}>{c.description}{c.required ? " (required)" : ""}</li>)}</ul>
      {r.performedAt && <p>Performed: {new Date(r.performedAt).toLocaleString()}</p>}{r.result && <p>{r.result}</p>}{r.note && <p>{r.note}</p>}
      {r.canAssign && <fieldset disabled={busy || uncertain}><label>Technician user ID<input type="number" min="1" value={assignees[r.runId] ?? ""} onChange={e => setAssignees({...assignees,[r.runId]:e.target.value})} /></label><button onClick={() => void change(r,false)}>Assign technician</button></fieldset>}
      {r.canCancel && <fieldset disabled={busy || uncertain}><label>Cancellation reason<textarea minLength={10} maxLength={4000} value={reasons[r.runId] ?? ""} onChange={e => setReasons({...reasons,[r.runId]:e.target.value})} /></label><button onClick={() => void change(r,true)}>Cancel occurrence</button></fieldset>}
    </details>)}
  </section>;
}
