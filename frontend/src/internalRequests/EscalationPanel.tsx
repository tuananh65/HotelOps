import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface View { requestId: number; version: string; timingStatus: string; canEscalate: boolean; hasOpen: boolean;
  createdAt: string; deadline: string | null; priorityId: number | null; priorities: { id: number; name: string }[];
  handlerId: number | null; handlers: { id: number; name: string }[] }
class Failure extends Error { constructor(readonly status: number) { super("Escalation failed"); } }
const version = (v: unknown): v is string => typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
const validDate = (v: unknown): v is string => typeof v === "string" && /Z$/.test(v) && Number.isFinite(Date.parse(v));
async function json(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  const response = await fetch(path, { ...init, credentials: "same-origin", cache: "no-store", redirect: "error", signal });
  if (!response.ok) throw new Failure(response.status);
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
  const [handler, setHandler] = useState("");
  const [denied, setDenied] = useState(false); const [message, setMessage] = useState(""); const [busy, setBusy] = useState(false);
  const sending = useRef(false); const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    setResult(null); setReason(""); setPriority(""); setDeadline(""); setHandler(""); setDenied(false); setMessage(""); setBusy(false); sending.current = false;
    const abort = new AbortController(); controller.current = abort;
    if (owner) void json(`/api/internal-requests/${id}/escalation`, abort.signal).then(raw => {
      const view = raw as View | null;
      if (!view || view.requestId !== id || !version(view.version) || !validDate(view.createdAt)
        || !(view.deadline === null || validDate(view.deadline)) || !(view.priorityId === null || Number.isSafeInteger(view.priorityId) && view.priorityId > 0)
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
  async function submit() {
    const abort = controller.current;
    if (!owner || !view?.canEscalate || sending.current || !abort || abort.signal.aborted) return;
    if (reason.trim().length < 10 || /[\u0000-\u001f\u007f]/.test(reason)) { setMessage("Enter a reason of at least 10 characters without control characters."); return; }
    if (!priority && !deadline && !handler) { setMessage("Select a new priority, deadline or handler."); return; }
    if (handler && !view.handlers.some(h => h.id === Number(handler))) { setMessage("Reload and select an eligible handler."); return; }
    const parsed = deadline ? new Date(deadline) : null;
    if (parsed && (!Number.isFinite(parsed.getTime()) || parsed.getTime() <= Date.parse(view.createdAt))) { setMessage("Deadline must be after official creation time."); return; }
    sending.current = true; setBusy(true); setMessage("");
    const current = () => !abort.signal.aborted && liveOwner.current === owner;
    try {
      const csrf = await json("/api/auth/csrf", abort.signal) as { csrfToken?: unknown };
      if (!current() || typeof csrf.csrfToken !== "string" || !csrf.csrfToken) throw new Error("Security check failed");
      const raw = await json(`/api/internal-requests/${id}/escalation`, abort.signal, {
        method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": `"${view.version}"` },
        body: JSON.stringify({ reason: reason.trim(), priorityId: priority ? Number(priority) : null, deadline: parsed?.toISOString() ?? null, handlerId: handler ? Number(handler) : null }),
      }) as { requestId?: unknown; eventId?: unknown; version?: unknown };
      if (raw.requestId !== id || !Number.isSafeInteger(raw.eventId) || Number(raw.eventId) <= 0 || !version(raw.version)) throw new Error("Unconfirmed escalation");
      if (current()) { setResult(null); setReason(""); setPriority(""); setDeadline(""); setHandler(""); onSaved(); }
    } catch (e) {
      if (current()) {
        setResult(null); setReason(""); setPriority(""); setDeadline(""); setHandler("");
        setMessage(e instanceof Failure && e.status === 412 ? "Request changed. Reload before escalating."
          : "Escalation not confirmed. Reload current access and state before retrying.");
        if (e instanceof Failure && e.status === 401) actions.invalidateSession(owner);
      }
    } finally { if (current()) { sending.current = false; setBusy(false); } }
  }
  if (!owner || denied) return null;
  return <section aria-label="Escalate request" aria-busy={busy}>
    {message && <p role="status">{message}</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload escalation</button>
    {view && <p>Created: {view.createdAt}. Deadline: {view.deadline ?? "Missing"}. Timing: {view.timingStatus}.</p>}
    {view?.hasOpen && <p role="status">This request already has an Open escalation.</p>}
    {view && !view.canEscalate && !view.hasOpen && <p>Escalation requires an active request at risk or overdue with a valid time window.</p>}
    {view?.canEscalate && <>
      <label>New priority<select aria-label="New priority" value={priority} disabled={busy} onChange={e => setPriority(e.target.value)}>
        <option value="">Keep current priority</option>{view.priorities.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
      </select></label>
      <label>New deadline (local time)<input type="datetime-local" value={deadline} disabled={busy} onChange={e => setDeadline(e.target.value)} /></label>
      {view.handlers.length > 0 && <label>New handler<select aria-label="Escalation handler" value={handler} disabled={busy} onChange={e => setHandler(e.target.value)}>
        <option value="">Keep current handler</option>{view.handlers.map(h => <option key={h.id} value={h.id}>{h.name}</option>)}
      </select></label>}
      <label>Escalation reason<input value={reason} maxLength={4000} disabled={busy} onChange={e => setReason(e.target.value)} /></label>
      <button disabled={busy} onClick={() => void submit()}>Escalate request</button>
    </>}
  </section>;
}
