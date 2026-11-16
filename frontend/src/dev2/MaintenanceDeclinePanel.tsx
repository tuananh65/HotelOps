import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function MaintenanceDeclinePanel({ data }: { data: Values }) {
  const { state } = useAuth(), session = state.session;
  const [bound] = useState(session), current = useRef(session); current.current = session;
  const [reason, setReason] = useState(""), [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false), [attempted, setAttempted] = useState(false), [message, setMessage] = useState("");
  const sending = useRef(false), writer = useRef<AbortController | null>(null);
  useEffect(() => () => writer.current?.abort(), [session, data]);
  const id = data.maintenanceWorkId, owner = data.assigneeId, tag = data.executionETag, actor = Number(session?.user.id);
  if (!session || session !== bound || data.canDecline !== true || data.status !== "PENDING"
    || typeof id !== "number" || !Number.isSafeInteger(id) || id <= 0
    || typeof owner !== "number" || !Number.isSafeInteger(owner) || owner <= 0 || owner !== actor
    || !Number.isSafeInteger(actor) || actor <= 0 || typeof tag !== "string" || !/^"[0-9A-F]{16}"$/.test(tag)) return null;
  async function submit() {
    if (reason.trim().length < 10 || reason.trim().length > 2000 || sending.current || attempted || current.current !== session) return;
    sending.current = true; setBusy(true); setAttempted(true); setMessage("");
    const abort = new AbortController(); writer.current = abort;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!bootstrap.ok) throw Error("Session check failed. Reload before retrying.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string"
        || !/^[\x21-\x7e]+$/.test(csrf.csrfToken) || abort.signal.aborted || current.current !== session)
        throw Error("Session changed. Reload before retrying.");
      const response = await fetch(`/api/maintenance-works/${id}/decline`, {
        method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": tag as string },
        body: JSON.stringify({ reason: reason.trim() })
      });
      if (!response.ok) throw Error(response.status === 412 ? "Work changed. Reload before declining again."
        : "Decline was not confirmed. Reload and check your assignment.");
      const result: unknown = await response.json();
      if (!isObject(result) || result.maintenanceWorkId !== id || result.workflow !== "PENDING_ASSIGNMENT"
        || typeof result.auditId !== "number" || !Number.isSafeInteger(result.auditId) || result.auditId <= 0
        || typeof result.declinedAt !== "string" || Number.isNaN(Date.parse(result.declinedAt)))
        throw Error("Decline response could not be confirmed. Reload to check current state.");
      if (!abort.signal.aborted && current.current === session) setMessage("Assignment declined. Pending Assignment. You can no longer start this assignment. Reload the list.");
    } catch (error) {
      if (!abort.signal.aborted && current.current === session) setMessage(error instanceof Error ? error.message : "Decline not confirmed. Reload.");
    } finally { if (!abort.signal.aborted) { setBusy(false); setReview(false); sending.current = false; } }
  }
  return <section aria-label="Decline maintenance assignment">
    <h3>Decline assignment</h3>
    <p>Declining clears your current assignment. SLA deadlines and room blocks remain unchanged.</p>
    {message && <p role="status">{message}</p>}
    <label>Decline reason<textarea maxLength={2000} disabled={busy || attempted || review} value={reason} onChange={e => setReason(e.target.value)} /></label>
    {review ? <><p>Decline this assignment and notify the Maintenance Lead?</p><p>{reason.trim()}</p>
      <button disabled={busy} onClick={() => void submit()}>Confirm decline</button><button disabled={busy} onClick={() => setReview(false)}>Cancel</button></>
      : <button disabled={reason.trim().length < 10 || busy || attempted} onClick={() => setReview(true)}>Review decline</button>}
  </section>;
}
