import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface RouteView {
  requestId: number; status: string; version: string;
  departments: { departmentId: number; name: string }[];
}
class Failure extends Error { constructor(readonly status: number) { super("Routing unavailable"); } }
const version = (v: unknown): v is string => typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
async function json(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  const r = await fetch(path, { ...init, credentials: "same-origin", cache: "no-store", redirect: "error", signal });
  if (!r.ok) throw new Failure(r.status);
  if (!r.headers.get("content-type")?.includes("application/json")) throw new Error("Invalid response");
  return r.json();
}
export function InternalRequestRoutePanel({ id, onRouted }: { id: number; onRouted: () => void }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.pending === null && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [result, setResult] = useState<{ owner: typeof owner; id: number; view: RouteView } | null>(null);
  const [revision, setRevision] = useState(0);
  const [department, setDepartment] = useState("");
  const [note, setNote] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    const abort = new AbortController(); controller.current = abort;
    setResult(null); setDepartment(""); setNote(""); setMessage(""); setBusy(false); sending.current = false;
    if (owner) void json(`/api/internal-requests/${id}/route`, abort.signal).then(raw => {
      const v = raw as RouteView;
      if (!v || v.requestId !== id || typeof v.status !== "string" || !version(v.version)
        || !Array.isArray(v.departments) || v.departments.some(d => !Number.isSafeInteger(d.departmentId) || d.departmentId <= 0 || typeof d.name !== "string")) throw new Error("Invalid route response");
      if (!abort.signal.aborted) setResult({ owner, id, view: v });
    }).catch(e => {
      if (abort.signal.aborted) return;
      setMessage("Routing unavailable. Reload to check current access.");
      if (e instanceof Failure && e.status === 401) actions.invalidateSession(owner!);
    });
    return () => abort.abort();
  }, [id, owner, revision, actions]);
  const view = owner && result?.owner === owner && result.id === id ? result.view : null;
  async function routeRequest() {
    const abort = controller.current;
    if (!owner || !view || !abort || abort.signal.aborted || sending.current || view.status !== "PENDING_ACCEPTANCE") return;
    const selected = view.departments.find(d => d.departmentId === Number(department));
    if (!selected || note.trim().length < 10) { setMessage("Select a target department and enter a reason of at least 10 characters."); return; }
    sending.current = true; setBusy(true); setMessage("");
    try {
      const csrf = await json("/api/auth/csrf", abort.signal) as { csrfToken?: unknown };
      if (typeof csrf.csrfToken !== "string" || !csrf.csrfToken || abort.signal.aborted) throw new Error("Security check failed");
      const raw = await json(`/api/internal-requests/${id}/route`, abort.signal, {
        method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": `"${view.version}"` },
        body: JSON.stringify({ departmentId: selected.departmentId, reason: note.trim() }),
      }) as { requestId?: unknown; departmentId?: unknown; version?: unknown };
      if (raw.requestId !== id || raw.departmentId !== selected.departmentId || !version(raw.version)) throw new Error("Route not confirmed");
      if (!abort.signal.aborted) {
        setResult(null); setDepartment(""); setNote("");
        onRouted();
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        setResult(null); setMessage(e instanceof Failure && e.status === 412 ? "Request changed. Reload departments before routing." : "Route not confirmed. Reload before retrying.");
        if (e instanceof Failure && e.status === 401) actions.invalidateSession(owner);
      }
    } finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); } }
  }
  if (!owner) return null;
  return <section aria-label="Route request">
    <h3>Route to another department</h3>
    {message && <p role="status">{message}</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload departments</button>
    {view && (view.status === "PENDING_ACCEPTANCE" ? <div aria-busy={busy}>
      <label>Target department<select aria-label="Target department" disabled={busy} value={department} onChange={e => setDepartment(e.target.value)}>
        <option value="">Select department</option>{view.departments.map(d => <option key={d.departmentId} value={d.departmentId}>{d.name}</option>)}
      </select></label>
      <label>Routing reason<input disabled={busy} maxLength={4000} value={note} onChange={e => setNote(e.target.value)} /></label>
      <button disabled={busy || !department} onClick={() => void routeRequest()}>Route request</button>
    </div> : <p>Only Pending Acceptance requests can be routed.</p>)}
  </section>;
}