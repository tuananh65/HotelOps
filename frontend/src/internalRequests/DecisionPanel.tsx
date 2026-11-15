import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { InternalRequestAssignmentPanel } from "./AssignmentPanel";
import { InternalRequestRoutePanel } from "./RoutePanel";

interface DecisionView {
  requestId: number; subject: string; description: string; status: string;
  submittedAt: string; deadline: string | null; ageSeconds: number;
  version: string; canDecide: boolean;
}
class DecisionFailure extends Error { constructor(readonly status: number) { super("Decision request failed"); } }
const version = (v: unknown): v is string => typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
const utc = (v: unknown): v is string => typeof v === "string" && /(?:Z|\+00:00)$/.test(v) && Number.isFinite(Date.parse(v));
function decode(value: unknown, id: number): DecisionView {
  const v = value as DecisionView | null;
  if (!v || v.requestId !== id || typeof v.subject !== "string" || typeof v.description !== "string"
    || typeof v.status !== "string" || !utc(v.submittedAt) || !(v.deadline === null || utc(v.deadline))
    || !Number.isFinite(v.ageSeconds) || v.ageSeconds < 0 || !version(v.version) || typeof v.canDecide !== "boolean")
    throw new Error("Invalid decision response");
  return v;
}
async function json(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  const response = await fetch(path, { ...init, credentials: "same-origin", cache: "no-store", redirect: "error", signal });
  if (!response.ok) throw new DecisionFailure(response.status);
  if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("Invalid response");
  return response.json();
}

export function InternalRequestDecisionPanel({ id }: { id: number }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.pending === null && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState<{ owner: typeof owner; id: number; view: DecisionView } | null>(null);
  const [reason, setReason] = useState("");
  const [deadline, setDeadline] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    setResult(null); setReason(""); setDeadline(""); setMessage(""); setBusy(false); sending.current = false;
    const abort = new AbortController(); controller.current = abort;
    if (owner) void json(`/api/internal-requests/${id}/decision`, abort.signal).then(v => decode(v, id)).then(view => {
      if (!abort.signal.aborted) setResult({ owner, id, view });
    }).catch(e => {
      if (abort.signal.aborted) return;
      setMessage("Request decision details unavailable. Reload to check current access and state.");
      if (e instanceof DecisionFailure && e.status === 401) actions.invalidateSession(owner);
    });
    return () => abort.abort();
  }, [id, owner, revision, actions]);
  const view = owner && result?.owner === owner && result.id === id ? result.view : null;
  async function decide(accept: boolean) {
    const abort = controller.current;
    if (!owner || !view?.canDecide || sending.current || !abort || abort.signal.aborted) return;
    if ((!accept || deadline) && !reason.trim()) { setMessage("A reason is required for rejection or a changed deadline."); return; }
    sending.current = true; setBusy(true); setMessage("");
    try {
      const csrf = await json("/api/auth/csrf", abort.signal) as { csrfToken?: unknown };
      if (typeof csrf.csrfToken !== "string" || !csrf.csrfToken || abort.signal.aborted) throw new Error("Security check failed");
      const adjustedDeadline = accept && deadline ? new Date(`${deadline}Z`).toISOString() : null;
      const raw = await json(`/api/internal-requests/${id}/decision`, abort.signal, {
        method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": `"${view.version}"` },
        body: JSON.stringify({ accept, reason: reason.trim() || null, deadline: adjustedDeadline }),
      }) as { requestId?: unknown; status?: unknown; version?: unknown; message?: unknown };
      if (raw.requestId !== id || raw.status !== (accept ? "ACCEPTED" : "REJECTED") || !version(raw.version)
        || raw.message !== (accept ? "MSG08" : "MSG19")) throw new Error("Decision not confirmed");
      if (!abort.signal.aborted) {
        setResult({ owner, id, view: { ...view, status: accept ? "ACCEPTED" : "REJECTED", deadline: adjustedDeadline ?? view.deadline, version: raw.version, canDecide: false } });
        setReason(""); setDeadline(""); setMessage(accept ? "MSG08: Request accepted." : "MSG19: Request rejected.");
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        setResult(null);
        setMessage(e instanceof DecisionFailure && e.status === 412 ? "Request changed. Reload before deciding." : "Decision not confirmed. Reload before retrying.");
        if (e instanceof DecisionFailure && e.status === 401) actions.invalidateSession(owner);
      }
    } finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); } }
  }
  if (!owner) return null;
  return <section aria-label="Request decision">
    {message && <p role="status">{message}</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload decision</button>
    {view && <>
      <h3>{view.subject}</h3><p>{view.description}</p><p>Status: {view.status}</p>
      <p>Submitted: {view.submittedAt}; age: {Math.floor(view.ageSeconds / 60)} minutes.</p>
      <p>Deadline (UTC): {view.deadline ?? "No deadline"}</p>
      {view.canDecide && <InternalRequestRoutePanel id={id} onRouted={() => {
        setResult(null); setMessage("MSG08: Request routed. Reload decision to check the new department and your current access.");
      }} />}
      {(view.status === "ACCEPTED" || view.status === "IN_PROGRESS") && <InternalRequestAssignmentPanel id={id} />}
      {view.canDecide && <div aria-busy={busy}>
        <label>Decision reason<input value={reason} maxLength={4000} disabled={busy} onChange={e => setReason(e.target.value)} /></label>
        <label>Adjusted deadline (UTC)<input type="datetime-local" value={deadline} disabled={busy} onChange={e => setDeadline(e.target.value)} /></label>
        <button disabled={busy} onClick={() => void decide(true)}>Accept request</button>
        <button disabled={busy} onClick={() => void decide(false)}>Reject request</button>
      </div>}
    </>}
  </section>;
}
