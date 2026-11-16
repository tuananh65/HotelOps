import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function MaintenanceResumePanel({ data, allowStart = false, allowHold = false, allowResume = true, onCommitted }: { data: Values; allowStart?: boolean; allowHold?: boolean; allowResume?: boolean; onCommitted?: (message: string) => void }) {
  const { state } = useAuth();
  const session = state.session;
  const current = useRef(session); current.current = session;
  const writer = useRef<AbortController | null>(null);
  const sending = useRef(false);
  const [busy, setBusy] = useState(false), [message, setMessage] = useState("");
  const [review, setReview] = useState(false), [attempted, setAttempted] = useState(false);
  const [reason, setReason] = useState("");
  useEffect(() => () => writer.current?.abort(), [session, data]);
  const id = data.maintenanceWorkId, tag = data.executionETag;
  const actor = Number(session?.user.id);
  const start = allowStart && data.canStart === true && data.status === "PENDING";
  const hold = allowHold && data.canHold === true && data.status === "IN_PROGRESS";
  const resumeAllowed = allowResume && data.canResume === true && (data.status === "ON_HOLD" || data.status === "IN_PROGRESS");
  const action = start ? "start" : hold ? "hold" : "resume", verb = start ? "Start" : hold ? "Hold" : "Resume";
  if (!session || (!start && !hold && !resumeAllowed) || typeof id !== "number"
      || !Number.isSafeInteger(id) || id <= 0 || !Number.isSafeInteger(actor) || actor <= 0
      || typeof tag !== "string" || !/^"[0-9A-F]{16}"$/.test(tag)) return null;
  async function resume() {
    if (sending.current || attempted || current.current !== session || !session) return;
    if (hold && reason.trim().length < 10) { setMessage("Enter a hold reason with at least 10 characters."); return; }
    sending.current = true; setBusy(true); setAttempted(true); setMessage("");
    const abort = new AbortController(); writer.current = abort;
    try {
      const csrfResponse = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!csrfResponse.ok) throw Error("Session check failed. Reload before retrying.");
      const csrf: unknown = await csrfResponse.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !csrf.csrfToken.trim()
          || /[\r\n]/.test(csrf.csrfToken) || current.current !== session || abort.signal.aborted) throw Error("Session changed. Reload before retrying.");
      const response = await fetch(`/api/maintenance-works/${id}/${action}`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": tag as string },
        body: JSON.stringify({ actorUserId: actor, ...(hold ? { reason: reason.trim() } : {}) }) });
      if (!response.ok) throw Error(response.status === 412 ? "Work changed. Reload its current state."
        : response.status === 409 ? `${verb} was not confirmed. Check your Working workday and other running work, then reload.`
        : `${verb} was not confirmed. Reload to check your current access and work state.`);
      const result: unknown = await response.json();
      if (!isObject(result) || result.maintenanceWorkId !== id || result.status !== (hold ? "ON_HOLD" : "IN_PROGRESS")
          || result.assigneeId !== actor || typeof result.executionETag !== "string"
          || !/^"[0-9A-F]{16}"$/.test(result.executionETag) || result.executionETag === tag)
        throw Error(`${verb} response could not be confirmed. Reload before any further action.`);
      if (current.current === session && !abort.signal.aborted) {
        const notice = `Work ${start ? "started" : hold ? "put on hold" : "resumed"}. ${hold ? "The work interval is closed; SLA time continues." : "A new work interval is open."}`;
        setMessage(notice);
        onCommitted?.(notice);
      }
    } catch (error) {
      if (current.current === session && !abort.signal.aborted) setMessage(error instanceof Error ? error.message : `${verb} not confirmed. Reload before retrying.`);
    } finally {
      if (!abort.signal.aborted) { sending.current = false; setBusy(false); setReview(false); }
    }
  }
  return <section aria-label={`${verb} maintenance`}>
    <p>{start ? "Accept and Start opens a work interval. A Working workday and no other running work are required." : hold ? "Hold stops the work interval. SLA time continues while work is on hold." : "Resume opens a new work interval. Start your workday first and stop any other running work."}</p>
    {hold && <label>Hold reason and note<textarea value={reason} disabled={busy || review || attempted} onChange={e => setReason(e.target.value)} /></label>}
    {message && <p role="status">{message}</p>}
    {!review ? <button type="button" disabled={busy || attempted || (hold && reason.trim().length < 10)} onClick={() => setReview(true)}>{verb} maintenance</button>
      : <><p>{verb} this assigned maintenance work now?</p>{hold && <p>{reason.trim()}</p>}<button type="button" disabled={busy} onClick={() => void resume()}>Confirm {action}</button>
        <button type="button" disabled={busy} onClick={() => setReview(false)}>Cancel</button></>}
  </section>;
}
