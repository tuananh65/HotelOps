import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

interface ReopenView {
  requestId: number; version: string; previousResult: string; closedAt: string;
  evaluatedAt: string; windowDays: number; lastHandlerId: number; canReopen: boolean; blocker: string | null;
}
class ReopenFailure extends Error {
  constructor(readonly status: number, readonly code: string) { super(code); }
}
const version = (v: unknown): v is string => typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
const time = (v: unknown): v is string => typeof v === "string" && Number.isFinite(Date.parse(v));
const displayTime = (v: string) => new Date(v).toLocaleString("en-GB", { timeZone: "Asia/Bangkok" }) + " (UTC+7)";
async function read(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  const response = await fetch(path, { ...init, credentials: "same-origin", cache: "no-store", redirect: "error", signal });
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { code?: unknown } | null;
    throw new ReopenFailure(response.status, typeof problem?.code === "string" ? problem.code : "REQUEST_FAILED");
  }
  if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("Invalid response");
  const value = await response.json() as { version?: unknown };
  if (path !== "/api/auth/csrf" && (!version(value?.version) || response.headers.get("etag") !== `"${value.version}"`))
    throw new Error("Version not confirmed");
  return value;
}

export function InternalRequestReopenPanel({ id, onReopened }: { id: number; onReopened: () => void }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.pending === null && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState<{ owner: typeof owner; id: number; view: ReopenView } | null>(null);
  const [reason, setReason] = useState("");
  const [message, setMessage] = useState("");
  const [hidden, setHidden] = useState(true);
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    setResult(null); setReason(""); setMessage(""); setBusy(false); setHidden(true); sending.current = false;
    const abort = new AbortController(); controller.current = abort;
    if (owner) void read(`/api/internal-requests/${id}/reopen`, abort.signal).then(raw => {
      const view = raw as ReopenView | null;
      if (!view || view.requestId !== id || !version(view.version) || typeof view.previousResult !== "string"
        || !time(view.closedAt) || !time(view.evaluatedAt) || !Number.isSafeInteger(view.windowDays) || view.windowDays <= 0
        || !Number.isSafeInteger(view.lastHandlerId) || view.lastHandlerId <= 0 || typeof view.canReopen !== "boolean"
        || !(view.blocker === null || typeof view.blocker === "string") || view.canReopen && view.blocker !== null)
        throw new Error("Invalid reopen response");
      if (!abort.signal.aborted) { setResult({ owner, id, view }); setHidden(false); }
    }).catch(e => {
      if (abort.signal.aborted) return;
      if (e instanceof ReopenFailure && e.code === "IR_NOT_CLOSED") return;
      setHidden(false); setMessage("Reopen availability could not be loaded. Reload current state and access.");
      if (e instanceof ReopenFailure && e.status === 401) actions.invalidateSession(owner);
    });
    return () => abort.abort();
  }, [id, owner, revision, actions]);
  const view = owner && result?.owner === owner && result.id === id ? result.view : null;
  async function reopen() {
    const abort = controller.current;
    if (!owner || !view?.canReopen || sending.current || !abort || abort.signal.aborted) return;
    const note = reason.trim();
    if (note.length < 10 || note.length > 4000 || /[\u0000-\u001f\u007f-\u009f]/.test(note)) {
      setMessage("Enter a reason of 10 to 4000 characters without control characters."); return;
    }
    sending.current = true; setBusy(true); setMessage("");
    try {
      const csrf = await read("/api/auth/csrf", abort.signal) as { csrfToken?: unknown };
      if (typeof csrf.csrfToken !== "string" || !csrf.csrfToken || abort.signal.aborted) throw new Error("Security check failed");
      const response = await read(`/api/internal-requests/${id}/reopen`, abort.signal, {
        method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": `"${view.version}"` },
        body: JSON.stringify({ reason: note }),
      }) as { requestId?: unknown; eventId?: number; status?: unknown; version?: unknown; messageCode?: unknown };
      if (response.requestId !== id || response.status !== "IN_PROGRESS" || !version(response.version)
        || !Number.isSafeInteger(response.eventId) || response.eventId! <= 0 || response.messageCode !== "MSG08") throw new Error("Reopen not confirmed");
      if (!abort.signal.aborted) { setResult(null); setReason(""); setHidden(true); onReopened(); }
    } catch (e) {
      if (!abort.signal.aborted) {
        setResult(null);
        setMessage(e instanceof ReopenFailure && e.code === "IR_REOPEN_WINDOW_EXPIRED" ? "MSG03: Reopen window expired. Create a new request."
          : e instanceof ReopenFailure && e.status === 412 ? "Request changed. Reload before reopening."
          : e instanceof ReopenFailure && e.status === 403 ? "MSG02: Reopen is not permitted with current access."
          : "Reopen not confirmed. Reload before retrying.");
        if (e instanceof ReopenFailure && e.status === 401) actions.invalidateSession(owner);
      }
    } finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); } }
  }
  if (!owner || hidden) return null;
  return <section aria-label="Reopen request" aria-busy={busy}>
    <h3>Reopen closed request</h3>
    {message && <p role="status">{message}</p>}
    {view && <>
      <p>Previous result: {view.previousResult}</p>
      <p>Closed: {displayTime(view.closedAt)}. Reopen window: {view.windowDays} days after closure.</p>
      <p>Server evaluation: {displayTime(view.evaluatedAt)}. Last handler: #{view.lastHandlerId}.</p>
      {view.blocker && <p role="status">{view.blocker === "IR_REOPEN_WINDOW_EXPIRED" ? "MSG03: Reopen window expired. Create a new request."
        : view.blocker === "IR_LAST_HANDLER_INELIGIBLE" ? "The last handler is no longer eligible. Reopen is unavailable."
        : view.blocker === "IR_REOPEN_REQUIRED" || view.blocker === "IR_AUTHORITY_CHANGED" ? "MSG02: Current reopen authority is required."
        : "Reopen storage is unavailable. Contact an administrator."}</p>}
      {view.canReopen && <>
        <p>Reopen returns this request to In Progress with its last handler and preserves previous results and handovers.</p>
        <label>Reopen reason<input value={reason} maxLength={4000} disabled={busy} onChange={e => setReason(e.target.value)} /></label>
        <button disabled={busy || reason.trim().length < 10} onClick={() => void reopen()}>Confirm Reopen</button>
      </>}
    </>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload reopen availability</button>{" "}
    <Link to="/internal-requests/new">Create a new request</Link>
  </section>;
}
