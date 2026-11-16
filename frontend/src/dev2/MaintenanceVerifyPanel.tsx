import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function MaintenanceVerifyPanel({ data }: { data: Values }) {
  const { state } = useAuth(), session = state.session;
  const [bound] = useState(session), current = useRef(session); current.current = session;
  const [note, setNote] = useState(""), [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false), [attempted, setAttempted] = useState(false), [message, setMessage] = useState("");
  const sending = useRef(false), writer = useRef<AbortController | null>(null);
  useEffect(() => () => writer.current?.abort(), [session, data]);
  const submitted = isObject(data.submittedReview) ? data.submittedReview.submissionAuditId : null;
  const id = data.maintenanceWorkId, owner = data.assigneeId, tag = data.executionETag, actor = Number(session?.user.id);
  if (typeof submitted !== "number" || !Number.isSafeInteger(submitted) || submitted <= 0
    || !session || session !== bound || data.canVerify !== true || data.status !== "COMPLETED"
    || typeof id !== "number" || !Number.isSafeInteger(id) || id <= 0
    || typeof owner !== "number" || !Number.isSafeInteger(owner) || owner <= 0 || owner === actor
    || !Number.isSafeInteger(actor) || actor <= 0 || typeof tag !== "string" || !/^"[0-9A-F]{16}"$/.test(tag)) return null;
  async function submit() {
    if (note.trim().length < 10 || sending.current || attempted || current.current !== session) return;
    sending.current = true; setBusy(true); setAttempted(true); setMessage("");
    const abort = new AbortController(); writer.current = abort;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!bootstrap.ok) throw Error("Session check failed. Reload before retrying.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string"
        || !/^[\x21-\x7e]+$/.test(csrf.csrfToken) || abort.signal.aborted || current.current !== session)
        throw Error("Session changed. Reload before retrying.");
      const response = await fetch(`/api/maintenance-works/${id}/verify`, {
        method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": tag as string },
        body: JSON.stringify({ actorUserId: actor, note: note.trim(), submissionAuditId: submitted })
      });
      if (!response.ok) throw Error(response.status === 412 ? "Work changed. Reload before reviewing again."
        : "Closure was not confirmed. Reload and check reviewer access, evidence and material usage.");
      const result: unknown = await response.json();
      if (!isObject(result) || result.maintenanceWorkId !== id || result.assigneeId !== owner || result.status !== "CLOSED"
        || typeof result.executionETag !== "string" || !/^"[0-9A-F]{16}"$/.test(result.executionETag) || result.executionETag === tag)
        throw Error("Closure response could not be confirmed. Reload to check current state.");
      if (!abort.signal.aborted && current.current === session) setMessage("Work and Incident closed. Room readiness is recalculated; a released Incident block does not mean the room is Ready. Reload the work detail.");
    } catch (error) {
      if (!abort.signal.aborted && current.current === session) setMessage(error instanceof Error ? error.message : "Closure not confirmed. Reload.");
    } finally { if (!abort.signal.aborted) { setBusy(false); setReview(false); sending.current = false; } }
  }
  return <section aria-label="Verify maintenance result">
    <h3>Accept and close</h3>
    <p>Review the submitted result, evidence and materials before closing. Other room blocks remain effective.</p>
    {message && <p role="status">{message}</p>}
    <label>Verification note<textarea disabled={busy || attempted || review} value={note} onChange={e => setNote(e.target.value)} /></label>
    {review ? <><p>Accept this submitted result and close the maintenance work and Incident?</p><p>{note.trim()}</p>
      <button disabled={busy} onClick={() => void submit()}>Confirm closure</button><button disabled={busy} onClick={() => setReview(false)}>Cancel</button></>
      : <button disabled={note.trim().length < 10 || busy || attempted} onClick={() => setReview(true)}>Review closure</button>}
  </section>;
}
