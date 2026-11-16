import { useEffect, useRef, useState } from "react";
import { PreventiveTechnicianSelect } from "./PreventiveTechnicianSelect";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import { Link } from "react-router-dom";

type Occurrence = { runId: number; scheduleId: number; title: string; instruction: string; status: string;
  dueAt: string; isOverdue: boolean; performedAt: string | null; result: string | null; note: string | null; assigneeId: number | null;
  assetId: number | null; roomId: number | null; areaId: number | null; version: string; canAssign: boolean; canCancel: boolean; canStart: boolean; canComplete: boolean;
  results: { key: string; passed: boolean; note: string | null }[] | null;
  checkItems: { key: string; description: string; required: boolean }[] };
const positive = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
type LinkedIncident = { incidentId: number; number: string; title: string; status: string; checkKey: string; linkedAt: string };
type History = { previousOccurrences: Occurrence[]; incidents: LinkedIncident[] };
type Evidence = { id: number; name: string; mimeType: string; size: number };
function valid(v: unknown): v is Occurrence {
  return isObject(v) && positive(v.runId) && positive(v.scheduleId) && typeof v.title === "string" && typeof v.instruction === "string"
    && typeof v.status === "string" && typeof v.dueAt === "string" && Number.isFinite(Date.parse(v.dueAt))
    && (v.performedAt === null || typeof v.performedAt === "string" && Number.isFinite(Date.parse(v.performedAt)))
    && [v.result,v.note].every(x => x === null || typeof x === "string")
    && [v.assigneeId,v.assetId,v.roomId,v.areaId].every(x => x === null || positive(x))
    && typeof v.version === "string" && /^"[0-9A-F]{16}"$/.test(v.version) && typeof v.canAssign === "boolean" && typeof v.canCancel === "boolean" && typeof v.canStart === "boolean" && typeof v.canComplete === "boolean"
    && (v.results === null || Array.isArray(v.results) && v.results.every(r => isObject(r) && typeof r.key === "string" && typeof r.passed === "boolean" && (r.note === null || typeof r.note === "string")))
    && Array.isArray(v.checkItems) && v.checkItems.every(c => isObject(c) && typeof c.key === "string"
      && typeof c.description === "string" && typeof c.required === "boolean");
}
export function PreventiveOccurrencesPanel({ branchId, runId }: { branchId: number; runId?: number }) {
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const mounted = useRef(owner).current, current = useRef(owner); current.current = owner;
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);
  const [rows,setRows] = useState<Occurrence[]>([]), [message,setMessage] = useState("");
  const [busy,setBusy] = useState(false), sending = useRef(false);
  const [assignees,setAssignees] = useState<Record<number,string>>({}), [reasons,setReasons] = useState<Record<number,string>>({});
  const [uncertain,setUncertain] = useState(false);
  const [hasMore,setHasMore] = useState(false);
  const [dueFrom,setDueFrom] = useState(""), [dueTo,setDueTo] = useState(""), [overdue,setOverdue] = useState("");
  const clearResults = () => { setRows([]); setHistory({}); setEvidence({}); };
  const [mine,setMine] = useState(true), [status,setStatus] = useState("OPEN");
  const [outcomes,setOutcomes] = useState<Record<string,string>>({}), [checkNotes,setCheckNotes] = useState<Record<string,string>>({});
  const [notes,setNotes] = useState<Record<number,string>>({});
  const [history,setHistory] = useState<Record<number,History>>({});
  const [uploads,setUploads] = useState<Record<number,File[]>>({}), [evidence,setEvidence] = useState<Record<number,Evidence[]>>({});
  async function loadEvidence(id: number) {
    if(!owner||owner!==mounted||sending.current)return;
    const abort=new AbortController();controller.current=abort;sending.current=true;setBusy(true);setMessage("");
    try{
      const response=await fetch(`/api/branches/${branchId}/preventive-occurrences/${id}/evidence`,{credentials:"same-origin",cache:"no-store",redirect:"error",signal:abort.signal});
      if(!response.ok)throw Error(`Evidence unavailable (${response.status}).`);
      const raw: unknown=await response.json();
      if(!Array.isArray(raw)||!raw.every(e=>isObject(e)&&positive(e.id)&&typeof e.name==="string"&&typeof e.mimeType==="string"&&positive(e.size)))throw Error("Evidence response could not be confirmed.");
      if(!abort.signal.aborted&&current.current===owner)setEvidence(old=>({...old,[id]:raw as Evidence[]}));
    }catch(e){if(!abort.signal.aborted&&current.current===owner){setEvidence({});setMessage(String(e));}}
    finally{sending.current=false;if(!abort.signal.aborted&&current.current===owner)setBusy(false);}
  }
  async function loadHistory(id: number) {
    if (!owner || owner !== mounted || sending.current) return;
    const abort = new AbortController(); controller.current = abort; sending.current=true; setBusy(true); setMessage("");
    try {
      const response=await fetch(`/api/branches/${branchId}/preventive-occurrences/${id}/history`,{credentials:"same-origin",cache:"no-store",redirect:"error",signal:abort.signal});
      if(!response.ok)throw Error(`History unavailable (${response.status}).`);
      const raw: unknown=await response.json();
      if(!isObject(raw)||!Array.isArray(raw.previousOccurrences)||!raw.previousOccurrences.every(valid)||!Array.isArray(raw.incidents)
        ||!raw.incidents.every(i=>isObject(i)&&positive(i.incidentId)&&[i.number,i.title,i.status,i.checkKey].every(x=>typeof x==="string")&&typeof i.linkedAt==="string"&&Number.isFinite(Date.parse(i.linkedAt))))
        throw Error("History response could not be confirmed.");
      if(!abort.signal.aborted&&current.current===owner)setHistory(old=>({...old,[id]:raw as History}));
    }catch(e){if(!abort.signal.aborted&&current.current===owner){setHistory({});setMessage(String(e));}}
    finally{sending.current=false;if(!abort.signal.aborted&&current.current===owner)setBusy(false);}
  }
  function incidentUrl(row: Occurrence, key: string) {
    const query = new URLSearchParams({action:"A004A-021",branchId:String(branchId),occurrenceId:String(row.runId),occurrenceVersion:row.version,occurrenceCheckKey:key});
    if(row.roomId)query.set("roomId",String(row.roomId));
    if(row.areaId)query.set("areaId",String(row.areaId));
    if(row.assetId)query.set("assetId",String(row.assetId));
    return `/operations/incidents?${query.toString()}`;
  }
  async function change(row: Occurrence, cancel: boolean, start = false, complete = false) {
    if (!owner || owner !== mounted || sending.current || uncertain) return;
    const assigneeId = Number(assignees[row.runId]), reason = reasons[row.runId]?.trim() ?? "";
    if (!start && !complete && (cancel ? reason.length < 10 || reason.length > 4000 : !positive(assigneeId))) { setMessage("Choose a technician, or provide a cancellation reason of at least 10 characters."); return; }
    const results = row.checkItems.filter(c => outcomes[`${row.runId}:${c.key}`]).map(c => ({key:c.key,passed:outcomes[`${row.runId}:${c.key}`] === "PASS",note:checkNotes[`${row.runId}:${c.key}`] ?? null}));
    if (complete && row.checkItems.some(c => c.required && !results.some(r => r.key === c.key))) { setMessage("Record every required check result."); return; }
    const abort = new AbortController(); controller.current = abort; sending.current = true; setBusy(true); setMessage("");
    let submitted = false;
    try {
      const selected=complete?(uploads[row.runId]??[]):[];
      if(selected.length>5||selected.some(f=>f.size<1||f.size>10*1024*1024||!["image/jpeg","image/png","application/pdf"].includes(f.type)))throw Error("Select at most five JPG, PNG or PDF files, up to 10 MiB each.");
      const files=await Promise.all(selected.map(async file=>{
        const bytes=new Uint8Array(await file.arrayBuffer());let binary="";
        for(let offset=0;offset<bytes.length;offset+=8192)binary+=String.fromCharCode(...bytes.subarray(offset,offset+8192));
        return {name:file.name,mimeType:file.type,base64:btoa(binary)};
      }));
      if(abort.signal.aborted||current.current!==owner)return;
      const bootstrap = await fetch("/api/auth/csrf", {credentials:"same-origin",cache:"no-store",redirect:"error",signal:abort.signal});
      if (!bootstrap.ok) throw Error("Session check failed.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !/^[\x21-\x7e]+$/.test(csrf.csrfToken)
        || abort.signal.aborted || current.current !== owner) throw Error("Session changed. Reload.");
      submitted = true;
      const response = await fetch(`/api/branches/${branchId}/preventive-occurrences/${row.runId}/${complete ? "complete" : start ? "start" : cancel ? "cancel" : "assign"}`, {
        method:"POST",credentials:"same-origin",cache:"no-store",redirect:"error",signal:abort.signal,
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":row.version},
        body:JSON.stringify(complete ? {results,note:notes[row.runId] ?? null,files} : start ? {} : cancel ? {reason} : {assigneeId}) });
      if (!response.ok) throw Error(response.status === 412 ? "Occurrence changed." : `Change not confirmed (${response.status}).`);
      const raw: unknown = await response.json();
      if (!valid(raw) || raw.runId !== row.runId) throw Error("Change response could not be confirmed.");
      if (!abort.signal.aborted && current.current === owner) { setRows(old => old.map(r => r.runId === raw.runId ? raw : r)); setMessage(complete ? "Occurrence completed; work interval closed." : start ? "Occurrence started; work interval opened." : cancel ? "Occurrence cancelled." : "Technician assigned."); }
    } catch(e) { if (!abort.signal.aborted && current.current === owner) { setMessage(String(e)+(submitted ? " Reload before another change." : "")); if(submitted)setUncertain(true); } }
    finally { sending.current=false; if(!abort.signal.aborted && current.current===owner)setBusy(false); }
  }
  async function load(more = false) {
    if (!owner || owner !== mounted || sending.current) return;
    const abort = new AbortController(); controller.current = abort;
    sending.current = true; setBusy(true); setMessage(""); setHistory({}); setEvidence({});
    try {
      const filters = new URLSearchParams();
      if(dueFrom) filters.set("dueFrom",new Date(dueFrom + "+07:00").toISOString());
      if(dueTo) filters.set("dueTo",new Date(dueTo + "+07:00").toISOString());
      if(overdue) filters.set("overdue",overdue);
      const response = await fetch(runId != null ? `/api/branches/${branchId}/preventive-occurrences/${runId}` : `/api/branches/${branchId}/preventive-occurrences?afterId=${more ? rows.at(-1)?.runId ?? 0 : 0}&mine=${mine}${status ? `&status=${encodeURIComponent(status)}` : ""}&${filters.toString()}`,
        { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!response.ok) throw Error(`Occurrences unavailable (${response.status}). Check current access and configuration.`);
      const payload: unknown = await response.json();
      const raw = runId != null ? [payload] : payload;
      if (!Array.isArray(raw) || !raw.every(valid)) throw Error("Occurrence details could not be confirmed.");
      if (!abort.signal.aborted && current.current === owner) { setRows(more ? [...rows,...raw] : raw); setHasMore(runId == null && raw.length === 50); if (!raw.length) setMessage("No further occurrences in your current scope."); }
    } catch (e) { if (!abort.signal.aborted && current.current === owner) { setRows([]); setMessage(String(e)); } }
    finally { sending.current = false; if (!abort.signal.aborted && current.current === owner) setBusy(false); }
  }
  if (!owner || owner !== mounted || !positive(branchId)) return null;
  return <section aria-label="Scheduled maintenance occurrences"><h2>Scheduled maintenance</h2>
    {runId == null && <fieldset disabled={busy}><legend>My work and branch occurrences</legend>
      <label><input type="checkbox" checked={mine} onChange={e=>{setMine(e.target.checked);setRows([]);setHistory({});setEvidence({});}} />Assigned to me</label>
      <label>Status<select value={status} onChange={e=>{setStatus(e.target.value);setRows([]);setHistory({});setEvidence({});}}>
        <option value="OPEN">Open work</option><option value="">All statuses</option><option value="PENDING_ASSIGNMENT">Pending assignment</option>
        <option value="ASSIGNED">Assigned</option><option value="IN_PROGRESS">In progress</option><option value="COMPLETED">Completed</option><option value="CANCELLED">Cancelled</option>
      </select></label>
      <label>Due from (UTC+7)<input type="datetime-local" value={dueFrom} onChange={e=>{setDueFrom(e.target.value);clearResults();}} /></label>
      <label>Due to (UTC+7)<input type="datetime-local" value={dueTo} onChange={e=>{setDueTo(e.target.value);clearResults();}} /></label>
      <label>Overdue<select value={overdue} onChange={e=>{setOverdue(e.target.value);clearResults();}}><option value="">All</option><option value="true">Overdue</option><option value="false">Not overdue</option></select></label>
      <p>Earliest due occurrences first. Branch results remain limited by your current permissions.</p>
    </fieldset>}
    <button disabled={busy} onClick={() => void load()}>{runId != null ? `Open occurrence #${runId}` : "Load occurrences"}</button>
    {runId == null && hasMore && !!rows.length && <button disabled={busy} onClick={() => void load(true)}>Load more occurrences</button>}
    {message && <p role="status">{message}</p>}
    {rows.map(r => <details key={r.runId} open={runId != null || undefined}><summary>{r.title} — {r.status} — {new Date(r.dueAt).toLocaleString()}</summary>
      <p>{r.isOverdue ? "Overdue" : "Not overdue"}</p><p>{r.instruction}</p><p>Occurrence #{r.runId}; schedule #{r.scheduleId}; assignee {r.assigneeId ?? "Pending assignment"}.</p>
      <button disabled={busy} onClick={() => void loadHistory(r.runId)}>Load previous occurrences and linked incidents</button>
      {history[r.runId] && <section aria-label={`History of occurrence ${r.runId}`}>
        <h3>Previous occurrences in your current scope</h3>
        {!history[r.runId].previousOccurrences.length && <p>No previous occurrences available in your scope.</p>}
        {history[r.runId].previousOccurrences.map(p => <details key={p.runId}><summary>{p.title} — {new Date(p.dueAt).toLocaleString()} — {p.status}</summary>
          <p>{p.instruction}</p>{p.note && <p>{p.note}</p>}
          {p.results && <ul>{p.results.map(c => <li key={c.key}>{p.checkItems.find(i=>i.key===c.key)?.description}: {c.passed ? "Pass" : "Fail"}{c.note && ` — ${c.note}`}</li>)}</ul>}
        </details>)}
        <h3>Linked incidents with current Incident access</h3>
        {!history[r.runId].incidents.length && <p>No linked incidents available in your scope.</p>}
        <ul>{history[r.runId].incidents.map(i => <li key={i.incidentId}><Link to={`/operations/incidents/${i.incidentId}?branchId=${branchId}`}>{i.number}: {i.title}</Link> — {i.status} — check {r.checkItems.find(c=>c.key===i.checkKey)?.description ?? i.checkKey}</li>)}</ul>
      </section>}
      {r.canStart && <button disabled={busy || uncertain} onClick={() => void change(r,false,true)}>Start occurrence</button>}
      {r.canComplete && <fieldset disabled={busy || uncertain}><legend>Record check results</legend>
        {r.checkItems.map(c => <div key={c.key}><label>{c.description}{c.required ? " (required)" : ""}<select value={outcomes[`${r.runId}:${c.key}`] ?? ""} onChange={e => setOutcomes({...outcomes,[`${r.runId}:${c.key}`]:e.target.value})}><option value="">Select result</option><option value="PASS">Pass</option><option value="FAIL">Fail</option></select></label>
          <label>Check note<textarea maxLength={2000} value={checkNotes[`${r.runId}:${c.key}`] ?? ""} onChange={e => setCheckNotes({...checkNotes,[`${r.runId}:${c.key}`]:e.target.value})} /></label>
          {outcomes[`${r.runId}:${c.key}`] === "FAIL" && !busy && !uncertain && <Link to={incidentUrl(r,c.key)}>Report Incident for this failed check</Link>}</div>)}
        <label>Completion note<textarea maxLength={4000} value={notes[r.runId] ?? ""} onChange={e => setNotes({...notes,[r.runId]:e.target.value})} /></label>
        <label>Photos or supporting files (up to five, 10 MiB each)<input type="file" multiple accept="image/jpeg,image/png,application/pdf" onChange={e=>setUploads({...uploads,[r.runId]:Array.from(e.target.files??[])})} /></label>
        <button onClick={() => void change(r,false,false,true)}>Complete occurrence</button>
      </fieldset>}
      {r.status==="COMPLETED"&&<><button disabled={busy} onClick={()=>void loadEvidence(r.runId)}>Load completion evidence</button>
        {evidence[r.runId]&&<ul>{evidence[r.runId].map(e=><li key={e.id}><a href={`/api/branches/${branchId}/preventive-occurrences/${r.runId}/evidence/${e.id}`} download>{e.name}</a> ({Math.ceil(e.size/1024)} KiB)</li>)}</ul>}</>}
      {r.results && <ul>{r.results.map(c => <li key={c.key}>{r.checkItems.find(i => i.key === c.key)?.description}: {c.passed ? "Pass" : "Fail"}{c.note && ` — ${c.note}`}</li>)}</ul>}
      <p>{r.roomId ? `Room #${r.roomId}` : r.areaId ? `Area #${r.areaId}` : `Asset #${r.assetId}`}</p>
      <ul>{r.checkItems.map(c => <li key={c.key}>{c.description}{c.required ? " (required)" : ""}</li>)}</ul>
      {r.performedAt && <p>Performed: {new Date(r.performedAt).toLocaleString()}</p>}{r.result && <p>{r.result}</p>}{r.note && <p>{r.note}</p>}
      {r.canAssign && <fieldset disabled={busy || uncertain}><PreventiveTechnicianSelect url={`/api/branches/${branchId}/preventive-occurrences/${r.runId}/technicians`} value={assignees[r.runId] ?? ""} onChange={value => setAssignees({...assignees,[r.runId]:value})} /><button onClick={() => void change(r,false)}>Assign technician</button></fieldset>}
      {r.canCancel && <fieldset disabled={busy || uncertain}><label>Cancellation reason<textarea minLength={10} maxLength={4000} value={reasons[r.runId] ?? ""} onChange={e => setReasons({...reasons,[r.runId]:e.target.value})} /></label><button onClick={() => void change(r,true)}>Cancel occurrence</button></fieldset>}
    </details>)}
  </section>;
}
