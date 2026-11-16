import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

type Context = { incidentId: number; previousWorkId: number; closedAt: string; closureNote: string;
  evaluatedAt: string; windowDays: number; version: string };
const positive = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const date = (v: unknown): v is string => typeof v === "string" && Number.isFinite(Date.parse(v));

export function IncidentReopenPanel({ data }: { data: Values }) {
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const mountedOwner = useRef(owner).current;
  const currentOwner = useRef(owner); currentOwner.current = owner;
  const id = data.incidentId;
  const [context, setContext] = useState<Context>();
  const [error, setError] = useState("");
  const [reason, setReason] = useState(""), [note, setNote] = useState(""), [discovery, setDiscovery] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [review, setReview] = useState(false), [busy, setBusy] = useState(false), [attempted, setAttempted] = useState(false);
  const [message, setMessage] = useState("");
  const sending = useRef(false), writer = useRef<AbortController | null>(null);
  useEffect(() => {
    if (!owner || owner !== mountedOwner || !positive(id) || data.status !== "CLOSED") return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/incidents/${id}/reopen`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        if (response.status === 403 || response.status === 404) return;
        if (!response.ok) throw Error(response.status === 409
          ? "Reopen is unavailable: the closure window or required closure evidence does not permit reopening."
          : "Reopen is unavailable. Reload to check the current session and configuration.");
        const raw: unknown = await response.json();
        if (!isObject(raw) || raw.incidentId !== id || !positive(raw.previousWorkId) || !date(raw.closedAt) || !date(raw.evaluatedAt)
          || typeof raw.closureNote !== "string" || !positive(raw.windowDays) || typeof raw.version !== "string" || !/^"[0-9A-F]{16}"$/.test(raw.version))
          throw Error("Reopen details could not be confirmed. Reload.");
        if (!abort.signal.aborted && currentOwner.current === owner) setContext(raw as Context);
      } catch (e) { if (!abort.signal.aborted && currentOwner.current === owner) setError(e instanceof Error ? e.message : "Reopen unavailable."); }
    })();
    return () => { abort.abort(); writer.current?.abort(); };
  }, [owner, mountedOwner, id, data.status]);

  const instant = new Date(discovery);
  const valid = context && reason.trim().length >= 10 && reason.length <= 4000 && note.length <= 4000
    && (note.trim().length > 0 || files.length > 0) && files.length <= 5
    && files.every(f => f.size > 0 && f.size <= 10 * 1024 * 1024 && ["image/jpeg", "image/png", "application/pdf"].includes(f.type))
    && Number.isFinite(instant.getTime()) && instant.getTime() >= Date.parse(context.closedAt) && instant.getTime() <= Date.now();
  async function submit() {
    if (!owner || currentOwner.current !== owner || !context || !valid || !review || sending.current || attempted) return;
    sending.current = true; setBusy(true); setAttempted(true);
    const abort = new AbortController(); writer.current = abort;
    try {
      const uploads = await Promise.all(files.map(async f => {
        const bytes = new Uint8Array(await f.arrayBuffer()); let binary = "";
        for (let offset = 0; offset < bytes.length; offset += 8192) binary += String.fromCharCode(...bytes.subarray(offset, offset + 8192));
        return { name: f.name, mimeType: f.type, base64: btoa(binary) };
      }));
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!bootstrap.ok) throw Error("Session check failed. Reload before retrying.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !/^[\x21-\x7e]+$/.test(csrf.csrfToken)
        || abort.signal.aborted || currentOwner.current !== owner) throw Error("Session changed. Reload.");
      const response = await fetch(`/api/incidents/${id}/reopen`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": context.version },
        body: JSON.stringify({ reason: reason.trim(), recurrenceDiscoveredAt: instant.toISOString(), evidence: { note: note.trim(), files: uploads } }) });
      if (!response.ok) throw Error(response.status === 412 ? "Incident changed. Reload before retrying."
        : response.status === 400 ? "Check the recurrence time, reason and evidence. Reload before retrying."
        : "Reopen was not confirmed. Reload the incident to check its current state and your permissions before retrying.");
      const result: unknown = await response.json();
      if (!isObject(result) || result.incidentId !== id || !positive(result.eventId) || result.workflow !== "PENDING_TRIAGE"
        || typeof result.version !== "string" || !/^"[0-9A-F]{16}"$/.test(result.version)) throw Error("Reopen response could not be confirmed. Reload the incident.");
      if (!abort.signal.aborted && currentOwner.current === owner)
        setMessage("Incident reopened to Pending Triage. Previous handling history is preserved. Reload the incident to classify, triage and assign the new cycle.");
    } catch (e) { if (!abort.signal.aborted && currentOwner.current === owner) setMessage(e instanceof Error ? e.message : "Reopen not confirmed. Reload."); }
    finally { if (!abort.signal.aborted) { setBusy(false); setReview(false); sending.current = false; } }
  }
  if (!owner || owner !== mountedOwner || !positive(id) || data.status !== "CLOSED") return null;
  if (!context) return error ? <p role="status">{error}</p> : null;
  return <section aria-label="Reopen incident"><h3>Reopen incident</h3>
    <p>Closed: {new Date(context.closedAt).toLocaleString()}. Reopen window: {context.windowDays} days after closure.</p>
    <p>{context.closureNote}</p>
    <fieldset disabled={busy || attempted || review}>
      <label>Recurrence discovered at (local time)<input type="datetime-local" step="1" value={discovery} onChange={e => setDiscovery(e.target.value)} /></label>
      <label>Reason (at least 10 characters)<textarea maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></label>
      <label>Evidence of recurrence<textarea maxLength={4000} value={note} onChange={e => setNote(e.target.value)} /></label>
      <label>Evidence files (up to 5 JPG, PNG or PDF files; 10 MiB each)<input type="file" multiple accept=".jpg,.jpeg,.png,.pdf" onChange={e => setFiles(Array.from(e.target.files ?? []))} /></label>
    </fieldset>
    {message && <p role="status">{message}</p>}
    {review ? <><p>Start a new processing cycle using this confirmed recurrence time? Previous repair records remain unchanged; the new SLA will be set during Triage.</p>
      <button disabled={busy || attempted} onClick={() => void submit()}>Confirm Reopen</button><button disabled={busy} onClick={() => setReview(false)}>Back</button></>
      : <button disabled={!valid || busy || attempted} onClick={() => setReview(true)}>Review Reopen</button>}
  </section>;
}
