import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function MaintenanceReturnPanel({ data, onCommitted }: { data: Values; onCommitted?: (message: string) => void }) {
  const { state } = useAuth(), session = state.session;
  const [bound] = useState(session), current = useRef(session); current.current = session;
  const [reason, setReason] = useState(""), [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false), [attempted, setAttempted] = useState(false), [message, setMessage] = useState("");
  const sending = useRef(false), writer = useRef<AbortController | null>(null);
  useEffect(() => () => writer.current?.abort(), [session, data]);
  const id = data.maintenanceWorkId, owner = data.assigneeId, tag = data.executionETag, actor = Number(session?.user.id);
  if (!session || session !== bound || data.canReturn !== true || data.status !== "COMPLETED"
    || typeof id !== "number" || !Number.isSafeInteger(id) || id <= 0
    || typeof owner !== "number" || !Number.isSafeInteger(owner) || owner <= 0 || owner === actor
    || !Number.isSafeInteger(actor) || actor <= 0 || typeof tag !== "string" || !/^"[0-9A-F]{16}"$/.test(tag)) return null;
  async function submit() {
    if (reason.trim().length < 10 || sending.current || attempted || current.current !== session) return;
    sending.current = true; setBusy(true); setAttempted(true); setMessage("");
    const abort = new AbortController(); writer.current = abort;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!bootstrap.ok) throw Error("Session check failed. Reload before retrying.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string"
        || !/^[\x21-\x7e]+$/.test(csrf.csrfToken) || abort.signal.aborted || current.current !== session)
        throw Error("Session changed. Reload before retrying.");
      const response = await fetch(`/api/maintenance-works/${id}/return`, {
        method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": tag as string },
        body: JSON.stringify({ actorUserId: actor, reason: reason.trim() })
      });
      if (!response.ok) throw Error(response.status === 412 ? "Work changed. Reload before reviewing again."
        : "Return was not confirmed. Reload and check reviewer access and the current assignee.");
      const result: unknown = await response.json();
      if (!isObject(result) || result.maintenanceWorkId !== id || result.assigneeId !== owner || result.status !== "IN_PROGRESS"
        || typeof result.executionETag !== "string" || !/^"[0-9A-F]{16}"$/.test(result.executionETag) || result.executionETag === tag)
        throw Error("Return response could not be confirmed. Reload to check current state.");
      if (!abort.signal.aborted && current.current === session) {
        const notice = "Result returned. The executor must Resume to start a new work interval.";
        setMessage(notice);
        onCommitted?.(notice);
      }
    } catch (error) {
      if (!abort.signal.aborted && current.current === session) setMessage(error instanceof Error ? error.message : "Return not confirmed. Reload.");
    } finally { if (!abort.signal.aborted) { setBusy(false); setReview(false); sending.current = false; } }
  }
  return <section aria-label="Return maintenance result">
    <h3>Return result for more work</h3>
    <p>Review the submitted result below. Returning preserves room blocks and does not start executor effort.</p>
    {message && <p role="status">{message}</p>}
    <label>Return reason<textarea disabled={busy || attempted || review} value={reason} onChange={e => setReason(e.target.value)} /></label>
    {review ? <><p>Return this result to its current assignee?</p><p>{reason.trim()}</p>
      <button disabled={busy} onClick={() => void submit()}>Confirm return</button><button disabled={busy} onClick={() => setReview(false)}>Cancel</button></>
      : <button disabled={reason.trim().length < 10 || busy || attempted} onClick={() => setReview(true)}>Review return</button>}
  </section>;
}
