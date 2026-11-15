import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface View { requestId: number; status: string; version: string; percent: number | null; note: string | null;
  result: string | null; deadline: string | null; canUpdate: boolean; canChangeDeadline: boolean; }
const version = (v: unknown): v is string => typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
class Failure extends Error { constructor(readonly status: number) { super("Progress unavailable"); } }
async function json(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  const response = await fetch(path, { ...init, credentials: "same-origin", cache: "no-store", redirect: "error", signal });
  if (!response.ok) throw new Failure(response.status);
  if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("Invalid response");
  return response.json();
}
export function InternalRequestProgressPanel({ id }: { id: number }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.pending === null && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [loaded, setLoaded] = useState<{ owner: typeof owner; id: number; view: View } | null>(null);
  const [revision, setRevision] = useState(0);
  const [percent, setPercent] = useState(""); const [note, setNote] = useState(""); const [result, setResult] = useState("");
  const [deadline, setDeadline] = useState(""); const [reason, setReason] = useState("");
  const [message, setMessage] = useState(""); const [busy, setBusy] = useState(false);
  const sending = useRef(false); const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    const abort = new AbortController(); controller.current = abort;
    setLoaded(null); setPercent(""); setNote(""); setResult(""); setDeadline(""); setReason(""); setMessage(""); setBusy(false); sending.current = false;
    if (owner) void json(`/api/internal-requests/${id}/progress`, abort.signal).then(raw => {
      const v = raw as View;
      if (!v || v.requestId !== id || typeof v.status !== "string" || !version(v.version)
        || !(v.percent === null || Number.isInteger(v.percent) && v.percent >= 0 && v.percent <= 100)
        || ![v.note, v.result, v.deadline].every(t => t === null || typeof t === "string")
        || v.deadline !== null && !Number.isFinite(Date.parse(v.deadline))
        || typeof v.canUpdate !== "boolean" || typeof v.canChangeDeadline !== "boolean") throw new Error("Invalid progress response");
      if (!abort.signal.aborted) { setLoaded({ owner, id, view: v }); setPercent(String(v.percent ?? 0)); setNote(v.note ?? ""); setResult(v.result ?? ""); }
    }).catch(e => { if (!abort.signal.aborted) { setMessage("Progress unavailable. Reload to check current access."); if (e instanceof Failure && e.status === 401) actions.invalidateSession(owner!); } });
    return () => abort.abort();
  }, [id, owner, revision, actions]);
  const view = owner && loaded?.owner === owner && loaded.id === id ? loaded.view : null;
  async function save() {
    const abort = controller.current;
    if (!owner || !view?.canUpdate || view.status !== "IN_PROGRESS" || !abort || abort.signal.aborted || sending.current) return;
    const value = Number(percent);
    if (!percent.trim() || !Number.isInteger(value) || value < 0 || value > 100) { setMessage("Enter progress from 0 to 100."); return; }
    if (deadline && (!view.canChangeDeadline || !reason.trim() || !Number.isFinite(Date.parse(deadline + "Z")))) { setMessage("A valid deadline and reason require department management authority."); return; }
    sending.current = true; setBusy(true); setMessage("");
    try {
      const csrf = await json("/api/auth/csrf", abort.signal) as { csrfToken?: unknown };
      if (typeof csrf.csrfToken !== "string" || !csrf.csrfToken || abort.signal.aborted) throw new Error("Security check failed");
      const raw = await json(`/api/internal-requests/${id}/progress`, abort.signal, { method: "POST",
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": `"${view.version}"` },
        body: JSON.stringify({ percent: value, note: note.trim() || null, result: result.trim() || null,
          deadline: view.canChangeDeadline && deadline ? new Date(deadline + "Z").toISOString() : null, deadlineReason: reason.trim() || null }) }) as { requestId?: unknown; status?: unknown; version?: unknown };
      if (raw.requestId !== id || raw.status !== "IN_PROGRESS" || !version(raw.version)) throw new Error("Progress not confirmed");
      if (!abort.signal.aborted) { setLoaded(null); setMessage("MSG08: Progress saved. Request remains In Progress. Reload progress to view the saved result."); }
    } catch (e) { if (!abort.signal.aborted) { setLoaded(null); setMessage(e instanceof Failure && e.status === 412 ? "Request changed. Reload progress before saving." : "Progress not confirmed. Reload before retrying."); if (e instanceof Failure && e.status === 401) actions.invalidateSession(owner); } }
    finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); } }
  }
  if (!owner) return null;
  return <section aria-label="Request progress"><h3>Progress and result</h3>
    {message && <p role="status">{message}</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload progress</button>
    {view && <><p>Saved progress: {view.percent === null ? "Not recorded" : `${view.percent}%`}</p>
      {view.note && <p>{view.note}</p>}{view.result && <p>{view.result}</p>}
      {view.deadline && <p>Deadline (UTC): {view.deadline}</p>}
      {view.canUpdate && view.status === "IN_PROGRESS" && <div aria-busy={busy}>
        <label>Progress percent<input type="number" min={0} max={100} step={1} disabled={busy} value={percent} onChange={e => setPercent(e.target.value)} /></label>
        <label>Progress note<textarea aria-label="Progress note" maxLength={4000} disabled={busy} value={note} onChange={e => setNote(e.target.value)} /></label>
        <label>Result note<textarea aria-label="Result note" maxLength={4000} disabled={busy} value={result} onChange={e => setResult(e.target.value)} /></label>
        {view.canChangeDeadline && <><label>New deadline (UTC)<input type="datetime-local" disabled={busy} value={deadline} onChange={e => setDeadline(e.target.value)} /></label>
          <label>Deadline change reason<textarea aria-label="Deadline change reason" maxLength={4000} disabled={busy} value={reason} onChange={e => setReason(e.target.value)} /></label></>}
        <p>100% marks readiness for review; it does not close the request.</p>
        <button disabled={busy} onClick={() => void save()}>Save progress</button>
      </div>}</>}
  </section>;
}
