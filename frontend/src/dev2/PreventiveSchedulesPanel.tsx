import { useEffect, useRef, useState } from "react";
import { PreventiveTechnicianSelect } from "./PreventiveTechnicianSelect";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Check = { key: string; description: string; required: boolean };
type Schedule = { scheduleId: number; scheduleCode: string; title: string | null; instruction: string | null;
  frequency: string; interval: number; nextDueAt: string; assetId: number | null; roomId: number | null;
  areaId: number | null; defaultAssigneeId: number | null; isActive: boolean; version: string; checkItems: Check[] | null };
type Target = { id: number; kind: string; label: string };
const positive = (x: unknown): x is number => typeof x === "number" && Number.isSafeInteger(x) && x > 0;
function schedule(x: unknown): x is Schedule {
  return isObject(x) && positive(x.scheduleId) && typeof x.scheduleCode === "string"
    && (x.title === null || typeof x.title === "string") && (x.instruction === null || typeof x.instruction === "string")
    && typeof x.frequency === "string" && positive(x.interval) && typeof x.nextDueAt === "string" && Number.isFinite(Date.parse(x.nextDueAt))
    && [x.assetId, x.roomId, x.areaId, x.defaultAssigneeId].every(v => v === null || positive(v))
    && typeof x.isActive === "boolean" && typeof x.version === "string" && /^"[0-9A-F]{16}"$/.test(x.version)
    && (x.checkItems === null || Array.isArray(x.checkItems) && x.checkItems.every(c => isObject(c)
      && typeof c.key === "string" && typeof c.description === "string" && typeof c.required === "boolean"));
}
export function PreventiveSchedulesPanel({ branchId }: { branchId: number }) {
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const mounted = useRef(owner).current, current = useRef(owner); current.current = owner;
  const abort = useRef(new AbortController());
  useEffect(() => { const controller = new AbortController(); abort.current = controller; return () => controller.abort(); }, []);
  const [rows, setRows] = useState<Schedule[]>([]), [targets, setTargets] = useState<Target[]>([]);
  const [selected, setSelected] = useState<Schedule>();
  const [kind, setKind] = useState("ROOM"), [target, setTarget] = useState("");
  const [title, setTitle] = useState(""), [instruction, setInstruction] = useState("");
  const [frequency, setFrequency] = useState("DAYS"), [interval, setInterval] = useState("1");
  const [due, setDue] = useState(""), [assignee, setAssignee] = useState("");
  const [checks, setChecks] = useState<Check[]>([]), [reason, setReason] = useState("");
  const [message, setMessage] = useState(""), [busy, setBusy] = useState(false), [authorized, setAuthorized] = useState(false);
  const [uncertain, setUncertain] = useState(false); const sending = useRef(false);
  const [cursor, setCursor] = useState(0);
  const root = `/api/branches/${branchId}/preventive-schedules`;
  const live = () => owner !== null && current.current === owner && !abort.current.signal.aborted;
  async function request(url: string, init?: RequestInit) {
    const response = await fetch(url, { ...init, credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.current.signal });
    if (!response.ok) throw Error(response.status === 412 ? "Schedule changed. Reload before editing." : `Request unavailable (${response.status}). Check current access and configuration.`);
    return await response.json() as unknown;
  }
  async function load(more = false) {
    if (!live() || sending.current) return;
    sending.current = true; setBusy(true); setMessage("");
    try {
      const raw = await request(`${root}?afterId=${more ? cursor : 0}&limit=50`);
      if (!Array.isArray(raw) || !raw.every(schedule)) throw Error("Invalid schedule response.");
      if (live()) { setRows(more ? [...rows.filter(r => !raw.some(n => n.scheduleId === r.scheduleId)), ...raw].sort((a,b) => a.scheduleId-b.scheduleId) : raw); setCursor(raw.at(-1)?.scheduleId ?? (more ? cursor : 0)); setAuthorized(true); }
    } catch (e) { if (live()) { setAuthorized(false); setRows([]); setSelected(undefined); setMessage(String(e)); } }
    finally { sending.current = false; if (live()) setBusy(false); }
  }
  async function loadTargets(more = false) {
    if (!live() || sending.current) return;
    sending.current = true; setBusy(true);
    try {
      const raw = await request(`${root}/targets?kind=${kind}&afterId=${more ? targets.at(-1)?.id ?? 0 : 0}`);
      if (!Array.isArray(raw) || !raw.every(x => isObject(x) && positive(x.id) && x.kind === kind && typeof x.label === "string")) throw Error("Invalid targets.");
      if (live()) setTargets(more ? [...targets, ...raw as Target[]] : raw as Target[]);
    } catch (e) { if (live()) { setTargets([]); setTarget(""); setMessage(String(e)); } }
    finally { sending.current = false; if (live()) setBusy(false); }
  }
  function edit(row?: Schedule) {
    setSelected(row); setTitle(row?.title ?? ""); setInstruction(row?.instruction ?? "");
    setFrequency(row?.frequency ?? "DAYS"); setInterval(String(row?.interval ?? 1));
    setAssignee(row?.defaultAssigneeId?.toString() ?? ""); setChecks(row?.checkItems ?? []); setReason(""); setDue("");
  }
  async function save(deactivate = false) {
    if (!live() || sending.current || uncertain) return;
    if (!deactivate && (!title.trim() || !instruction.trim() || !positive(Number(interval))
      || (!selected && (!targets.some(t => t.id === Number(target)) || !due || !Number.isFinite(Date.parse(due))))
      || (assignee !== "" && !positive(Number(assignee))))) { setMessage("Complete the required schedule fields."); return; }
    if (deactivate && (!selected || reason.trim().length < 10)) { setMessage("Provide a deactivation reason of at least 10 characters."); return; }
    sending.current = true; setBusy(true); setMessage(""); let submitted = false;
    try {
      const csrf = await request("/api/auth/csrf");
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !/^[\x21-\x7e]+$/.test(csrf.csrfToken) || !live()) throw Error("Session changed. Reload.");
      const body = deactivate ? { reason } : { title, instruction, frequency, interval: Number(interval),
        firstDueAt: selected?.nextDueAt ?? new Date(due).toISOString(),
        assetId: selected ? selected.assetId : kind === "ASSET" ? Number(target) : null,
        roomId: selected ? selected.roomId : kind === "ROOM" ? Number(target) : null,
        areaId: selected ? selected.areaId : kind === "AREA" ? Number(target) : null,
        defaultAssigneeId: assignee === "" ? null : Number(assignee), checkItems: checks };
      submitted = true;
      const raw = await request(root + (selected ? `/${selected.scheduleId}` : "") + (deactivate ? "/deactivate" : ""), {
        method: selected && !deactivate ? "PUT" : "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken,
          ...(selected ? { "If-Match": selected.version } : {}) }, body: JSON.stringify(body) });
      if (!schedule(raw)) throw Error("Save response could not be confirmed.");
      if (live()) { setRows(old => [...old.filter(r => r.scheduleId !== raw.scheduleId), raw].sort((a,b) => a.scheduleId-b.scheduleId)); edit(raw); setMessage("Schedule saved. Existing occurrences retain their original definition."); }
    } catch (e) { if (live()) { setMessage(String(e) + (submitted ? " Reload this page before another change." : "")); if (submitted) setUncertain(true); } }
    finally { sending.current = false; if (live()) setBusy(false); }
  }
  if (!owner || owner !== mounted || !positive(branchId)) return null;
  return <section aria-label="Preventive maintenance schedules"><h2>Preventive maintenance schedules</h2>
    <button disabled={busy} onClick={() => void load()}>Load schedules</button>
    {rows.length > 0 && <button disabled={busy} onClick={() => void load(true)}>Load next page</button>}
    {message && <p role="status">{message}</p>}
    <ul>{rows.map(r => <li key={r.scheduleId}><button disabled={busy || uncertain} onClick={() => edit(r)}>{r.title ?? r.scheduleCode}</button> — {r.isActive ? "Active" : "Inactive"} — {new Date(r.nextDueAt).toLocaleString()}</li>)}</ul>
    {authorized && <><button disabled={busy || uncertain} onClick={() => edit()}>New schedule</button>
      <fieldset disabled={busy || uncertain || selected?.isActive === false || selected?.title === null}>
        {!selected && <><label>Target type<select value={kind} onChange={e => { setKind(e.target.value); setTargets([]); setTarget(""); }}><option>ROOM</option><option>AREA</option><option>ASSET</option></select></label>
          <button onClick={() => void loadTargets()}>Load targets</button><button disabled={!targets.length} onClick={() => void loadTargets(true)}>More targets</button>
          <label>Target<select value={target} onChange={e => setTarget(e.target.value)}><option value="">Select target</option>{targets.map(t => <option key={t.id} value={t.id}>{t.label}</option>)}</select></label></>}
        <label>Title<input maxLength={200} disabled={!!selected} value={title} onChange={e => setTitle(e.target.value)} /></label>
        <label>Work description<textarea maxLength={16000} value={instruction} onChange={e => setInstruction(e.target.value)} /></label>
        <label>Repeat every<input type="number" min="1" value={interval} onChange={e => setInterval(e.target.value)} /></label>
        <select aria-label="Interval unit" value={frequency} onChange={e => setFrequency(e.target.value)}><option>DAYS</option><option>WEEKS</option><option>MONTHS</option></select>
        {!selected && <label>First due (local time)<input type="datetime-local" value={due} onChange={e => setDue(e.target.value)} /></label>}
        <PreventiveTechnicianSelect url={`${root}/technicians`} value={assignee} onChange={setAssignee} optional />
        <p>The selected employee must currently have maintenance read and execution authority in this branch.</p>
        {checks.map((c,i) => <div key={c.key}><input aria-label={`Check ${i+1}`} maxLength={1000} value={c.description} onChange={e => setChecks(checks.map((x,j) => i===j ? {...x,description:e.target.value} : x))} />
          <label><input type="checkbox" checked={c.required} onChange={e => setChecks(checks.map((x,j) => i===j ? {...x,required:e.target.checked} : x))} />Required</label>
          <button onClick={() => setChecks(checks.filter((_,j) => j!==i))}>Remove check</button></div>)}
        <button disabled={checks.length>=100} onClick={() => setChecks([...checks,{key:crypto.randomUUID(),description:"",required:true}])}>Add check item</button>
        <button onClick={() => void save()}>Save schedule</button>
      </fieldset>
      {selected?.isActive && <fieldset disabled={busy || uncertain}><label>Deactivation reason<textarea maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></label><button onClick={() => void save(true)}>Deactivate schedule</button></fieldset>}
    </>}
  </section>;
}
