import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface AssignmentView {
  requestId: number; status: string; version: string; handlerId: number | null;
  handlers: { userId: number; name: string; openRequests: number }[];
}
class Failure extends Error { constructor(readonly status: number) { super("Assignment unavailable"); } }
const version = (v: unknown): v is string => typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
async function json(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  const r = await fetch(path, { ...init, credentials: "same-origin", cache: "no-store", redirect: "error", signal });
  if (!r.ok) throw new Failure(r.status);
  if (!r.headers.get("content-type")?.includes("application/json")) throw new Error("Invalid response");
  return r.json();
}
export function InternalRequestAssignmentPanel({ id }: { id: number }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.pending === null && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [result, setResult] = useState<{ owner: typeof owner; id: number; view: AssignmentView } | null>(null);
  const [revision, setRevision] = useState(0);
  const [handler, setHandler] = useState("");
  const [note, setNote] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    const abort = new AbortController(); controller.current = abort;
    setResult(null); setHandler(""); setNote(""); setMessage(""); setBusy(false); sending.current = false;
    if (owner) void json(`/api/internal-requests/${id}/assignment`, abort.signal).then(raw => {
      const v = raw as AssignmentView;
      if (!v || v.requestId !== id || typeof v.status !== "string" || !version(v.version)
        || !(v.handlerId === null || Number.isSafeInteger(v.handlerId) && v.handlerId > 0)
        || !Array.isArray(v.handlers) || v.handlers.some(h => !Number.isSafeInteger(h.userId) || h.userId <= 0 || typeof h.name !== "string" || !Number.isSafeInteger(h.openRequests) || h.openRequests < 0)) throw new Error("Invalid assignment response");
      if (!abort.signal.aborted) setResult({ owner, id, view: v });
    }).catch(e => {
      if (abort.signal.aborted) return;
      setMessage("Handler assignment unavailable. Reload to check current access.");
      if (e instanceof Failure && e.status === 401) actions.invalidateSession(owner!);
    });
    return () => abort.abort();
  }, [id, owner, revision, actions]);
  const view = owner && result?.owner === owner && result.id === id ? result.view : null;
  async function assign() {
    const abort = controller.current;
    if (!owner || !view || !abort || abort.signal.aborted || sending.current || !["ACCEPTED", "IN_PROGRESS"].includes(view.status)) return;
    const selected = view.handlers.find(h => h.userId === Number(handler));
    if (!selected || selected.userId === view.handlerId) { setMessage("Select a different eligible handler."); return; }
    sending.current = true; setBusy(true); setMessage("");
    try {
      const csrf = await json("/api/auth/csrf", abort.signal) as { csrfToken?: unknown };
      if (typeof csrf.csrfToken !== "string" || !csrf.csrfToken || abort.signal.aborted) throw new Error("Security check failed");
      const raw = await json(`/api/internal-requests/${id}/assignment`, abort.signal, {
        method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": `"${view.version}"` },
        body: JSON.stringify({ handlerId: selected.userId, note: note.trim() || null }),
      }) as { requestId?: unknown; status?: unknown; version?: unknown };
      if (raw.requestId !== id || raw.status !== "IN_PROGRESS" || !version(raw.version)) throw new Error("Assignment not confirmed");
      if (!abort.signal.aborted) {
        setResult(null); setHandler(""); setNote("");
        setMessage("MSG08: Handler assigned. Request is In Progress. Reload handlers before another change.");
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        setResult(null); setMessage(e instanceof Failure && e.status === 412 ? "Request changed. Reload handlers before assigning." : "Assignment not confirmed. Reload before retrying.");
        if (e instanceof Failure && e.status === 401) actions.invalidateSession(owner);
      }
    } finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); } }
  }
  if (!owner) return null;
  return <section aria-label="Assign request handler">
    <h3>Assign handler</h3>
    {message && <p role="status">{message}</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload handlers</button>
    {view && (["ACCEPTED", "IN_PROGRESS"].includes(view.status) ? <div aria-busy={busy}>
      <p>Current handler: {view.handlers.find(h => h.userId === view.handlerId)?.name ?? (view.handlerId ? "Unavailable" : "Unassigned")}</p>
      <label>Request handler<select aria-label="Request handler" disabled={busy} value={handler} onChange={e => setHandler(e.target.value)}>
        <option value="">Select handler</option>{view.handlers.map(h => <option key={h.userId} value={h.userId} disabled={h.userId === view.handlerId}>{h.name} ({h.openRequests} open requests)</option>)}
      </select></label>
      <label>Assignment note<input disabled={busy} maxLength={4000} value={note} onChange={e => setNote(e.target.value)} /></label>
      <button disabled={busy || !handler} onClick={() => void assign()}>Assign request handler</button>
    </div> : <p>Accept the request before assigning a handler.</p>)}
  </section>;
}
