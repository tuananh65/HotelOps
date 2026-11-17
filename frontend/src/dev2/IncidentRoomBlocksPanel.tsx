import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
type Room = { roomId: number; number: string; readiness: string; inScope: boolean; blockedByIncident: boolean };
type Context = { incidentId: number; version: string; page: number; hasMore: boolean; rooms: Room[] };
const positive = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
export function IncidentRoomBlocksPanel({ incidentId, onCommitted }: { incidentId: number; onCommitted?: (notice: string) => void }) {
  const { state } = useAuth(), owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const active = useRef<AbortController | null>(null), sending = useRef(false);
  const [result, setResult] = useState<{ owner: typeof owner; data: Context } | null>(null);
  const [selected, setSelected] = useState<number[]>([]), [action, setAction] = useState("BLOCK"), [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false), [review, setReview] = useState(false), [attempted, setAttempted] = useState(false);
  const [message, setMessage] = useState<{ owner: typeof owner; text: string } | null>(null);
  useEffect(() => () => active.current?.abort(), [owner, incidentId]);
  const data = result?.owner === owner && result.data.incidentId === incidentId ? result.data : null;
  async function load(page: number) {
    if (!owner || sending.current) return;
    sending.current = true; setBusy(true); setResult(null); setSelected([]); setReview(false); const abort = new AbortController(); active.current = abort;
    try {
      const response = await fetch(`/api/incidents/${incidentId}/room-blocks?page=${page}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!response.ok) throw Error("Room scope unavailable. Check current permission and incident state.");
      const r: unknown = await response.json();
      if (!isObject(r) || r.incidentId !== incidentId || r.page !== page || typeof r.hasMore !== "boolean" || typeof r.version !== "string" || !/^"[0-9A-F]{16}"$/.test(r.version)
        || !Array.isArray(r.rooms) || !r.rooms.every(v => isObject(v) && positive(v.roomId) && typeof v.number === "string" && typeof v.readiness === "string" && typeof v.inScope === "boolean" && typeof v.blockedByIncident === "boolean")) throw Error("Invalid room scope response.");
      if (!abort.signal.aborted && current.current === owner) { setResult({ owner, data: r as Context }); setSelected([]); setReview(false); setAttempted(false); setMessage(null); }
    } catch (e) { if (!abort.signal.aborted && current.current === owner) setMessage({ owner, text: e instanceof Error ? e.message : "Room scope unavailable." }); }
    finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); } }
  }
  const eligible = (r: Room) => action === "MAP" ? !r.inScope : r.inScope && (action === "BLOCK" ? !r.blockedByIncident : r.blockedByIncident);
  const valid = !!data && selected.length > 0 && selected.every(id => data.rooms.some(r => r.roomId === id && eligible(r))) && reason.trim().length >= 10;
  async function save() {
    if (!owner || !data || !valid || !review || sending.current || attempted) return;
    sending.current = true; setBusy(true); setAttempted(true); const abort = new AbortController(); active.current = abort;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!bootstrap.ok) throw Error("Session check failed. Reload.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !/^[\x21-\x7e]+$/.test(csrf.csrfToken) || current.current !== owner || abort.signal.aborted) throw Error("Session changed. Reload.");
      const response = await fetch(`/api/incidents/${incidentId}/room-blocks`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": data.version }, body: JSON.stringify({ action, roomIds: selected, reason: reason.trim() }) });
      if (response.status === 400) {
        const problem: unknown = await response.json();
        if (isObject(problem) && problem.title === "Invalid room block change" && !abort.signal.aborted && current.current === owner) {
          setAttempted(false);
          setMessage({ owner, text: typeof problem.detail === "string" ? problem.detail : "MSG03: Check selected rooms, existing blocks and reason." });
          return;
        }
      }
      if (!response.ok) throw Error(response.status === 412 ? "Incident changed. Reload rooms before retrying." : "Room change not confirmed. Reload rooms and check scope, reason and existing blocks.");
      const saved: unknown = await response.json();
      if (!isObject(saved) || saved.incidentId !== incidentId || saved.action !== action || typeof saved.version !== "string" || !/^"[0-9A-F]{16}"$/.test(saved.version)) throw Error("Response could not be confirmed. Reload rooms.");
      if (!abort.signal.aborted && current.current === owner) {
        const notice = action === "MAP" ? "MSG08: Affected-room scope saved. Reload rooms to block them." : "MSG08: Room blocks updated and readiness recalculated. Notifications queued for eligible supervisors.";
        setResult(null); setSelected([]); setMessage({ owner, text: notice }); onCommitted?.(notice);
      }
    } catch (e) { if (!abort.signal.aborted && current.current === owner) setMessage({ owner, text: e instanceof Error ? e.message : "Change not confirmed. Reload rooms." }); }
    finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); setReview(false); } }
  }
  if (!owner || !positive(incidentId)) return null;
  return <section aria-label="Incident room blocks"><h3>Affected rooms and blocks</h3>
    <button disabled={busy || review} onClick={() => void load(data?.page ?? 1)}>Reload rooms</button>
    {message?.owner === owner && <p role="status">{message.text}</p>}
    {data && <><fieldset disabled={busy || attempted || review}><label>Action<select value={action} onChange={e => { setAction(e.target.value); setSelected([]); }}>
      <option value="BLOCK">Block affected rooms</option><option value="REMOVE">Remove Incident blocks</option><option value="MAP">Add rooms to affected scope</option></select></label>
      <p>RoomId and explicit affected-room mappings determine scope. Removing this Incident's block does not remove other blocks or bypass cleaning requirements.</p>
      {data.rooms.map(r => <label key={r.roomId}><input type="checkbox" disabled={!eligible(r)} checked={selected.includes(r.roomId)} onChange={e => setSelected(e.target.checked ? [...selected, r.roomId] : selected.filter(id => id !== r.roomId))} />
        Room {r.number}: {r.readiness}; {r.inScope ? "in affected scope" : "outside affected scope"}; {r.blockedByIncident ? "blocked by this Incident" : "no block from this Incident"}</label>)}
      <label>Reason (at least 10 characters)<textarea value={reason} onChange={e => setReason(e.target.value)} /></label></fieldset>
      <button disabled={busy || review || attempted || data.page <= 1} onClick={() => void load(data.page - 1)}>Previous rooms</button><button disabled={busy || review || attempted || !data.hasMore} onClick={() => void load(data.page + 1)}>Next rooms</button>
      {review ? <><p>{action === "REMOVE" ? "MSG33: " : ""}Confirm {action.toLowerCase()} for {selected.length} selected room(s)?</p><button disabled={busy} onClick={() => void save()}>Confirm room change</button><button disabled={busy} onClick={() => setReview(false)}>Cancel</button></>
        : <button disabled={!valid || busy || attempted} onClick={() => setReview(true)}>Review room change</button>}</>}
  </section>;
}
