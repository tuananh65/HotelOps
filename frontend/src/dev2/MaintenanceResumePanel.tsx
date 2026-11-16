import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function MaintenanceResumePanel({ data }: { data: Values }) {
  const { state } = useAuth();
  const session = state.session;
  const current = useRef(session); current.current = session;
  const writer = useRef<AbortController | null>(null);
  const sending = useRef(false);
  const [busy, setBusy] = useState(false), [message, setMessage] = useState("");
  const [review, setReview] = useState(false), [attempted, setAttempted] = useState(false);
  useEffect(() => () => writer.current?.abort(), [session, data]);
  const id = data.maintenanceWorkId, tag = data.executionETag;
  const actor = Number(session?.user.id);
  if (!session || data.canResume !== true || data.status !== "ON_HOLD" || typeof id !== "number"
      || !Number.isSafeInteger(id) || id <= 0 || !Number.isSafeInteger(actor) || actor <= 0
      || typeof tag !== "string" || !/^"[0-9A-F]{16}"$/.test(tag)) return null;
  async function resume() {
    if (sending.current || attempted || current.current !== session || !session) return;
    sending.current = true; setBusy(true); setAttempted(true); setMessage("");
    const abort = new AbortController(); writer.current = abort;
    try {
      const csrfResponse = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!csrfResponse.ok) throw Error("Session check failed. Reload before retrying.");
      const csrf: unknown = await csrfResponse.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !csrf.csrfToken.trim()
          || /[\r\n]/.test(csrf.csrfToken) || current.current !== session || abort.signal.aborted) throw Error("Session changed. Reload before retrying.");
      const response = await fetch(`/api/maintenance-works/${id}/resume`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": tag as string },
        body: JSON.stringify({ actorUserId: actor }) });
      if (!response.ok) throw Error(response.status === 412 ? "Work changed. Reload its current state."
        : response.status === 409 ? "Resume was not confirmed. Check your Working workday and other running work, then reload."
        : "Resume was not confirmed. Reload to check your current access and work state.");
      const result: unknown = await response.json();
      if (!isObject(result) || result.maintenanceWorkId !== id || result.status !== "IN_PROGRESS"
          || result.assigneeId !== actor || typeof result.executionETag !== "string"
          || !/^"[0-9A-F]{16}"$/.test(result.executionETag) || result.executionETag === tag)
        throw Error("Resume response could not be confirmed. Reload before any further action.");
      if (current.current === session && !abort.signal.aborted) setMessage("Work resumed. Reload the work detail to view the current state.");
    } catch (error) {
      if (current.current === session && !abort.signal.aborted) setMessage(error instanceof Error ? error.message : "Resume not confirmed. Reload before retrying.");
    } finally {
      if (!abort.signal.aborted) { sending.current = false; setBusy(false); setReview(false); }
    }
  }
  return <section aria-label="Resume maintenance">
    <p>Resume opens a new work interval. Start your workday first and stop any other running work.</p>
    {message && <p role="status">{message}</p>}
    {!review ? <button type="button" disabled={busy || attempted} onClick={() => setReview(true)}>Resume maintenance</button>
      : <><p>Resume this assigned maintenance work now?</p><button type="button" disabled={busy} onClick={() => void resume()}>Confirm resume</button>
        <button type="button" disabled={busy} onClick={() => setReview(false)}>Cancel</button></>}
  </section>;
}
