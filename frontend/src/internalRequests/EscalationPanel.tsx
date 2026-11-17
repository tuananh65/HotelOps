import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface View { requestId: number; version: string; timingStatus: string; canEscalate: boolean; hasOpen: boolean;
  createdAt: string; deadline: string | null; priorityId: number | null; priorities: { id: number; name: string }[];
  status: string; evaluatedAt: string; departmentId: number | null; departments: { id: number; name: string; handlers: { id: number; name: string }[] }[];
  handlerId: number | null; handlers: { id: number; name: string }[] }
class Failure extends Error { constructor(readonly status: number, readonly code: string) { super("Escalation failed"); } }
const version = (v: unknown): v is string => typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
const validDate = (v: unknown): v is string => typeof v === "string" && /Z$/.test(v) && Number.isFinite(Date.parse(v));
async function json(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  const response = await fetch(path, { ...init, credentials: "same-origin", cache: "no-store", redirect: "error", signal });
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { code?: unknown } | null;
    throw new Failure(response.status, typeof problem?.code === "string" ? problem.code : "REQUEST_FAILED");
  }
  if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("Invalid response");
  return response.json();
}

export function InternalRequestEscalationPanel({ id, onSaved }: { id: number; onSaved: () => void }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.pending === null && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const liveOwner = useRef(owner); liveOwner.current = owner;
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState<{ owner: typeof owner; id: number; view: View } | null>(null);
  const [reason, setReason] = useState(""); const [priority, setPriority] = useState(""); const [deadline, setDeadline] = useState("");
  const [department, setDepartment] = useState("");
  const [handler, setHandler] = useState("");
  const [denied, setDenied] = useState(false); const [message, setMessage] = useState(""); const [busy, setBusy] = useState(false);
  const sending = useRef(false); const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    setResult(null); setReason(""); setPriority(""); setDeadline(""); setHandler(""); setDepartment(""); setDenied(false); setMessage(""); setBusy(false); sending.current = false;
    const abort = new AbortController(); controller.current = abort;
    if (owner) void json(`/api/internal-requests/${id}/escalation`, abort.signal).then(raw => {
      const view = raw as View | null;
      if (!view || view.requestId !== id || !version(view.version) || !validDate(view.createdAt)
        || !(view.deadline === null || validDate(view.deadline)) || !(view.priorityId === null || Number.isSafeInteger(view.priorityId) && view.priorityId > 0)
        || typeof view.status !== "string" || !validDate(view.evaluatedAt)
        || !(view.departmentId === null || Number.isSafeInteger(view.departmentId) && view.departmentId > 0)
        || !Array.isArray(view.departments) || !view.departments.every(d => d && Number.isSafeInteger(d.id) && d.id > 0 && d.id !== view.departmentId && typeof d.name === "string"
          && Array.isArray(d.handlers) && d.handlers.every(h => h && Number.isSafeInteger(h.id) && h.id > 0 && typeof h.name === "string"))
        || !(view.handlerId === null || Number.isSafeInteger(view.handlerId) && view.handlerId > 0)
        || !Array.isArray(view.handlers) || !view.handlers.every(h => h && Number.isSafeInteger(h.id) && h.id > 0 && h.id !== view.handlerId && typeof h.name === "string")
        || !["AT_RISK", "OVERDUE", "ON_TIME", "INVALID_WINDOW", "TERMINAL"].includes(view.timingStatus)
        || typeof view.canEscalate !== "boolean" || typeof view.hasOpen !== "boolean"
        || view.canEscalate && (view.hasOpen || !["AT_RISK", "OVERDUE"].includes(view.timingStatus))
        || !Array.isArray(view.priorities) || !view.priorities.every(p => p && Number.isSafeInteger(p.id) && p.id > 0 && typeof p.name === "string")) throw new Error("Invalid escalation response");
      if (!abort.signal.aborted && liveOwner.current === owner) setResult({ owner, id, view });
    }).catch(e => {
      if (abort.signal.aborted || liveOwner.current !== owner) return;
      if (e instanceof Failure && e.status === 403) setDenied(true);
      else setMessage("Escalation availability could not be loaded. Reload current access.");
      if (e instanceof Failure && e.status === 401) actions.invalidateSession(owner);
    });
    return () => abort.abort();
  }, [id, owner, revision, actions]);
  const view = owner && result?.owner === owner && result.id === id ? result.view : null;
  const targetDepartment = view?.departments.find(d => d.id === Number(department));
  const handlers = department ? targetDepartment?.handlers ?? [] : view?.handlers ?? [];
  async function submit() {
    const abort = controller.current;
    if (!owner || !view?.canEscalate || sending.current || !abort || abort.signal.aborted) return;
    if (reason.trim().length < 10 || /[\u0000-\u001f\u007f]/.test(reason)) { setMessage("Enter a reason of at least 10 characters without control characters."); return; }
    if (!priority && !deadline && !handler && !department) { setMessage("Select a new priority, deadline, receiving department or handler."); return; }
    if (department && view.status === "IN_PROGRESS" && !handler) { setMessage("Confirm an eligible handler in the new receiving department."); return; }
    if (handler && !handlers.some(h => h.id === Number(handler))) { setMessage("Reload and select an eligible handler."); return; }
    if (department && !view.departments.some(d => d.id === Number(department))) { setMessage("Reload and select an eligible receiving department."); return; }
    const parsed = deadline ? new Date(deadline) : null;
    if (parsed && (!Number.isFinite(parsed.getTime()) || parsed.getTime() <= Date.parse(view.createdAt))) { setMessage("Deadline must be after official creation time."); return; }
    sending.current = true; setBusy(true); setMessage("");
    const current = () => !abort.signal.aborted && liveOwner.current === owner;
    try {
      const csrf = await json("/api/auth/csrf", abort.signal) as { csrfToken?: unknown };
      if (!current() || typeof csrf.csrfToken !== "string" || !csrf.csrfToken) throw new Error("Security check failed");
      const raw = await json(`/api/internal-requests/${id}/escalation`, abort.signal, {
        method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": `"${view.version}"` },
        body: JSON.stringify({ reason: reason.trim(), priorityId: priority ? Number(priority) : null, deadline: parsed?.toISOString() ?? null, handlerId: handler ? Number(handler) : null, departmentId: department ? Number(department) : null }),
      }) as { requestId?: unknown; eventId?: unknown; version?: unknown };
      if (raw.requestId !== id || !Number.isSafeInteger(raw.eventId) || Number(raw.eventId) <= 0 || !version(raw.version)) throw new Error("Unconfirmed escalation");
      if (current()) { setResult(null); setReason(""); setPriority(""); setDeadline(""); setHandler(""); setDepartment(""); onSaved(); }
    } catch (e) {
      if (current()) {
        setResult(null); setReason(""); setPriority(""); setDeadline(""); setHandler(""); setDepartment("");
        setMessage(e instanceof Failure && e.status === 412 ? "Request changed. Reload before escalating."
          : e instanceof Failure && ["IR_ESCALATION_ALREADY_OPEN", "IR_ESCALATION_NOT_ELIGIBLE"].includes(e.code) ? "MSG03: Request is not eligible or already has an Open escalation. Reload current state."
          : e instanceof Failure && e.code === "IR_HANDOVER_PENDING" ? "A handover awaits acknowledgment. Resolve it before changing responsibility."
          : "Escalation not confirmed. Reload current access and state before retrying.");
        if (e instanceof Failure && e.status === 401) actions.invalidateSession(owner);
      }
    } finally { if (current()) { sending.current = false; setBusy(false); } }
  }
  if (!owner || denied) return null;
  return <section aria-label="Escalate request" aria-busy={busy}>
    {message && <p role="status">{message}</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload escalation</button>
    {view && <p>Created: {view.createdAt}. Age at server evaluation: {Date.parse(view.evaluatedAt) >= Date.parse(view.createdAt) ? ((Date.parse(view.evaluatedAt) - Date.parse(view.createdAt)) / 3600000).toFixed(1) + " hours" : "Unavailable"}. Deadline: {view.deadline ?? "Missing"}. Timing: {view.timingStatus}. Current handler: {view.handlerId === null ? "Unassigned" : `#${view.handlerId}`}.</p>}
    {view?.hasOpen && <p role="status">MSG03: This request already has an Open escalation.</p>}
    {view && !view.canEscalate && !view.hasOpen && <p>MSG03: Escalation requires an active request at risk or overdue with a valid time window.</p>}
    {view?.canEscalate && <>
      <label>New priority<select aria-label="New priority" value={priority} disabled={busy} onChange={e => setPriority(e.target.value)}>
        <option value="">Keep current priority</option>{view.priorities.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
      </select></label>
      <label>New deadline (local time)<input type="datetime-local" value={deadline} disabled={busy} onChange={e => setDeadline(e.target.value)} /></label>
      {handlers.length > 0 && <label>New handler<select aria-label="Escalation handler" value={handler} disabled={busy} onChange={e => setHandler(e.target.value)}>
        <option value="">{department ? "Select and confirm destination handler" : "Keep current handler"}</option>{handlers.map(h => <option key={h.id} value={h.id}>{h.name}</option>)}
      </select></label>}
      {view.departments.length > 0 && <label>New receiving department<select value={department} disabled={busy} onChange={e => { setDepartment(e.target.value); setHandler(""); }}>
        <option value="">Keep current department</option>{view.departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
      </select></label>}
      {department && view.status === "IN_PROGRESS" && <p>The request stays In Progress. Confirm a handler eligible for the destination department, including the current handler if eligible. Previous assignments and handovers are retained.</p>}
      {department && view.status === "IN_PROGRESS" && handlers.length === 0 && <p role="status">No eligible destination handler. Rerouting cannot proceed.</p>}
      {department && view.status === "ACCEPTED" && <p>The request returns to Pending Acceptance for the destination Department Head. Previous acceptance remains in history.</p>}
      {department && view.status === "PENDING_ACCEPTANCE" && <p>The request remains Pending Acceptance for the destination Department Head.</p>}
      <label>Escalation reason<input value={reason} maxLength={4000} disabled={busy} onChange={e => setReason(e.target.value)} /></label>
      <button disabled={busy || !!department && view.status === "IN_PROGRESS" && !handler} onClick={() => void submit()}>Escalate request</button>
    </>}
  </section>;
}
