import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface CompletionView { requestId: number; status: string; version: string; canComplete: boolean; blocker: string | null }
class CompletionFailure extends Error { constructor(readonly status: number) { super("Completion failed"); } }
const version = (v: unknown): v is string => typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
async function json(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  const response = await fetch(path, { ...init, credentials: "same-origin", cache: "no-store", redirect: "error", signal });
  if (!response.ok) throw new CompletionFailure(response.status);
  if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("Invalid response");
  return response.json();
}

export function InternalRequestCompletionPanel({ id, onCompleted }: { id: number; onCompleted: () => void }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.pending === null && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState<{ owner: typeof owner; id: number; view: CompletionView } | null>(null);
  const [resultNote, setResultNote] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    setResult(null); setResultNote(""); setMessage(""); setBusy(false); sending.current = false;
    const abort = new AbortController(); controller.current = abort;
    if (owner) void json(`/api/internal-requests/${id}/completion`, abort.signal).then(raw => {
      const view = raw as CompletionView | null;
      if (!view || view.requestId !== id || typeof view.status !== "string" || !version(view.version) || typeof view.canComplete !== "boolean" || !(view.blocker === null || typeof view.blocker === "string")
        || view.canComplete && view.status !== "IN_PROGRESS") throw new Error("Invalid completion response");
      if (!abort.signal.aborted) setResult({ owner, id, view });
    }).catch(e => {
      if (abort.signal.aborted) return;
      setMessage("Completion availability could not be loaded. Reload to check current access and state.");
      if (e instanceof CompletionFailure && e.status === 401) actions.invalidateSession(owner);
    });
    return () => abort.abort();
  }, [id, owner, revision, actions]);
  const view = owner && result?.owner === owner && result.id === id ? result.view : null;
  async function complete() {
    const abort = controller.current;
    if (!owner || !view?.canComplete || sending.current || !abort || abort.signal.aborted) return;
    if (!resultNote.trim()) { setMessage("Enter a completion result."); return; }
    sending.current = true; setBusy(true); setMessage("");
    try {
      const csrf = await json("/api/auth/csrf", abort.signal) as { csrfToken?: unknown };
      if (typeof csrf.csrfToken !== "string" || !csrf.csrfToken || abort.signal.aborted) throw new Error("Security check failed");
      const raw = await json(`/api/internal-requests/${id}/completion`, abort.signal, {
        method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": `"${view.version}"` },
        body: JSON.stringify({ result: resultNote.trim() }),
      }) as { requestId?: unknown; status?: unknown; version?: unknown; message?: unknown };
      if (raw.requestId !== id || raw.status !== "CLOSED" || !version(raw.version) || raw.message !== "MSG08") throw new Error("Completion not confirmed");
      if (!abort.signal.aborted) { setResult(null); setResultNote(""); setMessage("MSG08: Request closed."); onCompleted(); }
    } catch (e) {
      if (!abort.signal.aborted) {
        setResult(null);
        setMessage(e instanceof CompletionFailure && e.status === 412 ? "Request changed. Reload before closing."
          : e instanceof CompletionFailure && e.status === 403 ? "MSG02: Completion is not permitted. Reload current access."
          : "Completion not confirmed. Reload before retrying.");
        if (e instanceof CompletionFailure && e.status === 401) actions.invalidateSession(owner);
      }
    } finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); } }
  }
  if (!owner || view && !view.canComplete && !view.blocker) return null;
  return <section aria-label="Complete request" aria-busy={busy}>
    {message && <p role="status">{message}</p>}
    {view?.blocker && <p role="status">MSG34: Required work or handover is unresolved. Review the linked work below before closing.</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload completion</button>
    {view?.canComplete && <>
      <p>Confirm the result after all required linked work and handovers are complete.</p>
      <label>Completion result<input value={resultNote} maxLength={4000} disabled={busy} onChange={e => setResultNote(e.target.value)} /></label>
      <button disabled={busy} onClick={() => void complete()}>Complete request</button>
    </>}
  </section>;
}
