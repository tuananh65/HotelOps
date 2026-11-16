import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

type Option = { priorityId: number; name: string; responseDeadlineAt: string; resolutionDeadlineAt: string; targetVersion: string };
const positive = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const instant = (v: unknown) => typeof v === "string" && /Z$/.test(v) && Number.isFinite(Date.parse(v));
const option = (v: unknown): v is Option => isObject(v) && positive(v.priorityId) && typeof v.name === "string"
  && instant(v.responseDeadlineAt) && instant(v.resolutionDeadlineAt) && typeof v.targetVersion === "string" && /^[0-9A-F]{16}$/.test(v.targetVersion);
const utc = (v: string) => v && Number.isFinite(Date.parse(v + "+07:00")) ? new Date(v + "+07:00").toISOString() : undefined;
const displayTime = (v: string) => new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Ho_Chi_Minh", dateStyle: "short", timeStyle: "medium" }).format(new Date(v));
export function IncidentTriagePanel({ data }: { data: Values }) {
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const actor = state.session ? Number(state.session.user.id) : 0;
  const [priority, setPriority] = useState("");
  const [plan, setPlan] = useState(""), [reason, setReason] = useState("");
  const [response, setResponse] = useState(""), [resolution, setResolution] = useState("");
  const [review, setReview] = useState(false), [busy, setBusy] = useState(false), [attempted, setAttempted] = useState(false);
  const [saved, setSaved] = useState(false), [message, setMessage] = useState("");
  const current = useRef(owner), active = useRef<AbortController | null>(null), sending = useRef(false);
  current.current = owner;
  useEffect(() => () => active.current?.abort(), [owner]);
  const choices = Array.isArray(data.triageOptions) ? data.triageOptions.filter(option) : [];
  const chosen = choices.find(o => String(o.priorityId) === priority);
  const id = data.incidentId, tag = data.triageETag;
  if (!owner || !positive(actor) || !positive(id) || !positive(data.severityId) || data.canTriage !== true
    || typeof tag !== "string" || !/^"[0-9A-F]{16}"$/.test(tag)) return null;
  const changed = !!chosen && ((response !== "" && Date.parse(utc(response) ?? "") !== Date.parse(chosen.responseDeadlineAt))
    || (resolution !== "" && Date.parse(utc(resolution) ?? "") !== Date.parse(chosen.resolutionDeadlineAt)));
  const valid = !!chosen && plan.trim().length > 0 && (!response || !!utc(response)) && (!resolution || !!utc(resolution))
    && (!changed || reason.trim().length >= 10);
  async function save() {
    if (!valid || !review || !chosen || sending.current || attempted) return;
    sending.current = true; setBusy(true); setAttempted(true);
    const session = owner, abort = new AbortController(); active.current = abort;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!bootstrap.ok) throw Error("Session check failed. Reload before retrying.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string"
        || !/^[\x21-\x7e]+$/.test(csrf.csrfToken) || abort.signal.aborted || current.current !== session) throw Error("Session changed. Reload.");
      const res = await fetch(`/api/incidents/${id}/triage`, { method: "PATCH", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": tag as string },
        body: JSON.stringify({ actorUserId: actor, severityId: data.severityId, priorityId: chosen.priorityId,
          expectedSlaTargetVersion: chosen.targetVersion, initialActionPlan: plan.trim(), responseDeadlineAt: utc(response), resolutionDeadlineAt: utc(resolution),
          deadlineChangeReason: changed ? reason.trim() : null }) });
      if (!res.ok) throw Error(res.status === 412 ? "Incident or SLA targets changed. Reload before trying again." : "Triage was not confirmed. Reload and check current access and classification.");
      const result: unknown = await res.json();
      if (!isObject(result) || result.incidentId !== id || result.priorityId !== chosen.priorityId || result.severityId !== data.severityId
        || result.status !== "OPEN" || typeof result.triageETag !== "string" || !/^"[0-9A-F]{16}"$/.test(result.triageETag) || result.triageETag === tag
        || !isObject(result.sla) || !instant(result.sla.responseDeadlineAt) || !instant(result.sla.resolutionDeadlineAt)
        || Date.parse(String(result.sla.responseDeadlineAt)) !== Date.parse(utc(response) ?? chosen.responseDeadlineAt)
        || Date.parse(String(result.sla.resolutionDeadlineAt)) !== Date.parse(utc(resolution) ?? chosen.resolutionDeadlineAt))
        throw Error("Triage response could not be confirmed. Reload to check the current record.");
      if (!abort.signal.aborted && current.current === session) { setSaved(true); setMessage("Triage saved. The incident is pending assignment."); }
    } catch (e) { if (!abort.signal.aborted && current.current === session) setMessage(e instanceof Error ? e.message : "Triage not confirmed. Reload."); }
    finally { if (!abort.signal.aborted) { setBusy(false); setReview(false); sending.current = false; } }
  }
  return <section aria-label="Triage incident">
    <h3>Triage incident</h3>
    <fieldset disabled={busy || attempted || review}>
      <label>Priority<select aria-label="Priority" value={priority} onChange={e => { setPriority(e.target.value); setResponse(""); setResolution(""); setReason(""); }}>
        <option value="">Select priority</option>{choices.map(o => <option key={o.priorityId} value={o.priorityId}>{o.name}</option>)}</select></label>
      {chosen && <p>Default response deadline: {displayTime(chosen.responseDeadlineAt)}; resolution deadline: {displayTime(chosen.resolutionDeadlineAt)} (Asia/Ho_Chi_Minh, UTC+7).</p>}
      <label>Initial action plan<textarea value={plan} onChange={e => setPlan(e.target.value)} /></label>
      <label>Override response deadline (UTC+7, optional)<input type="datetime-local" step="1" value={response} onChange={e => setResponse(e.target.value)} /></label>
      <label>Override resolution deadline (UTC+7, optional)<input type="datetime-local" step="1" value={resolution} onChange={e => setResolution(e.target.value)} /></label>
      {changed && <label>Reason for changing deadlines (at least 10 characters)<textarea value={reason} onChange={e => setReason(e.target.value)} /></label>}
    </fieldset>
    {message && <p role="status">{message}</p>}
    {saved ? <Link to={`/operations/incidents/${id}?action=A004A-023`}>Reload incident to assign</Link>
      : review ? <><p>Confirm priority, deadlines and initial action plan?</p><button disabled={busy} onClick={() => void save()}>Confirm triage</button><button disabled={busy} onClick={() => setReview(false)}>Cancel</button></>
      : <button disabled={!valid || attempted || busy} onClick={() => setReview(true)}>Review triage</button>}
  </section>;
}
