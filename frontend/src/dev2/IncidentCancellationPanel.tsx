import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function IncidentCancellationPanel({ data, onCommitted, onReload }: { data: Values; onCommitted?: (notice: string) => void; onReload?: () => void }) {
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const [kind, setKind] = useState("INVALID"), [reason, setReason] = useState(""), [original, setOriginal] = useState("");
  const [review, setReview] = useState(false), [busy, setBusy] = useState(false), [attempted, setAttempted] = useState(false);
  const [message, setMessage] = useState<{ owner: typeof owner; text: string } | null>(null);
  const current = useRef(owner), sending = useRef(false), active = useRef<AbortController | null>(null);
  current.current = owner;
  useEffect(() => () => active.current?.abort(), [owner, data.incidentId]);
  const id = data.incidentId, tag = data.triageETag;
  const originalId = kind === "DUPLICATE" && /^[1-9][0-9]*$/.test(original) && Number.isSafeInteger(Number(original)) ? Number(original) : null;
  const valid = reason.trim().length >= 10 && (kind === "INVALID" || originalId !== null && originalId !== id);
  async function submit() {
    if (!owner || !valid || !review || sending.current || attempted || typeof tag !== "string") return;
    sending.current = true; setBusy(true); setAttempted(true);
    const abort = new AbortController(); active.current = abort;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!bootstrap.ok) throw Error("Session check failed. Reload before retrying.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !/^[\x21-\x7e]+$/.test(csrf.csrfToken)
        || current.current !== owner || abort.signal.aborted) throw Error("Session changed. Reload.");
      const response = await fetch(`/api/incidents/${id}/cancel-triage`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": tag },
        body: JSON.stringify({ kind, reason: reason.trim(), originalIncidentId: kind === "DUPLICATE" ? originalId : null }) });
      if (response.status === 400) {
        const problem: unknown = await response.json();
        if (isObject(problem) && problem.title === "Invalid cancellation" && !abort.signal.aborted && current.current === owner) {
          setAttempted(false);
          setMessage({ owner, text: typeof problem.detail === "string" ? problem.detail : "Check cancellation type, original incident and reason." });
          return;
        }
      }
      if (!response.ok) throw Error(response.status === 412 ? "Incident changed. Reload before retrying." : "Cancellation was not confirmed. Check current permissions, original incident and Pending Triage status, then reload.");
      const result: unknown = await response.json();
      if (!isObject(result) || result.incidentId !== id || result.status !== "CANCELLED" || typeof result.eventId !== "number" || !Number.isSafeInteger(result.eventId) || result.eventId <= 0)
        throw Error("Cancellation response could not be confirmed. Reload.");
      if (!abort.signal.aborted && current.current === owner) {
        const notice = "Incident cancelled. Its effective blocks were removed; room readiness was recalculated. Reporter notification is queued subject to current access.";
        setMessage({ owner, text: notice }); onCommitted?.(notice);
      }
    } catch (e) { if (!abort.signal.aborted && current.current === owner) setMessage({ owner, text: e instanceof Error ? e.message : "Cancellation not confirmed. Reload." }); }
    finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); setReview(false); } }
  }
  if (!owner || data.canCancelTriage !== true || typeof id !== "number" || !Number.isSafeInteger(id) || id <= 0 || typeof tag !== "string" || !/^"[0-9A-F]{16}"$/.test(tag)) return null;
  return <section aria-label="Cancel incident during Triage"><h3>Cancel as Duplicate / Invalid</h3>
    <fieldset disabled={busy || attempted || review}>
      <label>Cancellation type<select value={kind} onChange={e => setKind(e.target.value)}><option value="INVALID">Invalid</option><option value="DUPLICATE">Duplicate</option></select></label>
      {kind === "DUPLICATE" && <label>Original incident ID<input inputMode="numeric" value={original} onChange={e => setOriginal(e.target.value)} /></label>}
      <label>Reason (at least 10 characters)<textarea value={reason} onChange={e => setReason(e.target.value)} /></label>
    </fieldset>
    {message?.owner === owner && <p role="status">{message.text}</p>}
    {attempted && onReload && <button disabled={busy} onClick={onReload}>Reload incident</button>}
    {review ? <><p>MSG33: Cancel this incident as {kind.toLowerCase()}{originalId ? `, referencing original incident ${originalId}` : ""}? Only its effective room blocks will be removed. Other blocks and cleaning requirements remain.</p>
      <button disabled={busy} onClick={() => void submit()}>Confirm incident cancellation</button><button disabled={busy} onClick={() => setReview(false)}>Keep incident</button></>
      : <button disabled={!valid || busy || attempted} onClick={() => setReview(true)}>Review cancellation</button>}
  </section>;
}
