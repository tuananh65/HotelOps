import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Item = { escalationId: number; state: string; reason: string; requestedSupport: string; openedAt: string; closedAt: string | null };
type Context = { eTag: string | null; canCreate: boolean; severity: string | null; responseDeadlineAt: string | null; resolutionDeadlineAt: string | null;
  serverNow: string; timing: { classification: string; percentUsed: number | null; unavailableReason: string | null }; items: Item[] };
const date = (value: string | null) => value ? new Date(value).toLocaleString("en-GB", { timeZone: "Asia/Ho_Chi_Minh" }) + " UTC+7" : "N/A";

export function IncidentEscalationPanel({ id, onCommitted }: { id: number; onCommitted?: (notice: string) => void }) {
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const active = useRef<AbortController | null>(null), sending = useRef(false);
  const [snapshot, setSnapshot] = useState<{ owner: typeof owner; id: number; value: Context } | null>(null);
  const [message, setMessage] = useState<{ owner: typeof owner; text: string } | null>(null);
  const [reason, setReason] = useState(""), [support, setSupport] = useState("");
  const [busy, setBusy] = useState(false), [attempted, setAttempted] = useState(false), [review, setReview] = useState(false);
  const [reload, setReload] = useState(0);
  useEffect(() => {
    const abort = new AbortController(); active.current = abort;
    setSnapshot(null); setMessage(null); setAttempted(false); setReview(false); setReason(""); setSupport("");
    if (owner) {
      setBusy(true);
      void fetch(`/api/incidents/${id}/escalations`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal })
        .then(async response => {
          if (!response.ok) throw Error("Escalation details unavailable. Check current access and storage availability.");
          const data: unknown = await response.json();
          if (!isObject(data) || !isObject(data.timing) || !Array.isArray(data.items) || typeof data.canCreate !== "boolean") throw Error("Invalid escalation response.");
          if (!abort.signal.aborted && current.current === owner) setSnapshot({ owner, id, value: data as unknown as Context });
        }).catch(error => { if (!abort.signal.aborted && current.current === owner) setMessage({ owner, text: String(error.message) }); })
        .finally(() => { if (!abort.signal.aborted) setBusy(false); });
    }
    return () => { abort.abort(); active.current?.abort(); };
  }, [owner, id, reload]);
  const data = snapshot?.owner === owner && snapshot.id === id ? snapshot.value : null;
  const valid = reason.trim().length >= 10 && reason.trim().length <= 4000 && support.trim().length > 0 && support.trim().length <= 4000;
  async function submit() {
    if (!owner || !data?.canCreate || !data.eTag || !/^"[0-9A-F]{16}"$/.test(data.eTag) || !review || !valid || attempted || sending.current) return;
    sending.current = true; setBusy(true); setAttempted(true);
    const abort = new AbortController(); active.current = abort;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!bootstrap.ok) throw Error("Session check failed. Reload before retrying.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !/^[\x21-\x7e]+$/.test(csrf.csrfToken)
        || current.current !== owner || abort.signal.aborted) throw Error("Session changed. Reload.");
      const response = await fetch(`/api/incidents/${id}/escalations`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": data.eTag }, body: JSON.stringify({ reason: reason.trim(), requestedSupport: support.trim() }) });
      if (!response.ok) throw Error(response.status === 412 ? "Incident changed. Reload before retrying." : "Escalation not confirmed. Reload to check eligibility, permissions and any existing open escalation.");
      const saved: unknown = await response.json();
      if (!isObject(saved) || saved.incidentId !== id || saved.status !== "OPEN" || typeof saved.eventId !== "number" || !Number.isSafeInteger(saved.eventId) || saved.eventId <= 0) throw Error("Response not confirmed. Reload before retrying.");
      if (!abort.signal.aborted && current.current === owner) {
        const notice = "Escalation saved. Operations Manager notification is queued for eligible recipients and requires acknowledgment.";
        setSnapshot(null); setMessage({ owner, text: notice }); onCommitted?.(notice);
      }
    } catch (error) { if (!abort.signal.aborted && current.current === owner) setMessage({ owner, text: error instanceof Error ? error.message : "Escalation not confirmed. Reload." }); }
    finally { sending.current = false; if (!abort.signal.aborted) { setBusy(false); setReview(false); } }
  }
  if (!owner) return null;
  return <section aria-label="Incident escalation"><h3>Incident escalation</h3>
    <button disabled={busy} onClick={() => setReload(n => n + 1)}>Reload escalation</button>
    {message?.owner === owner && <p role="status">{message.text}</p>}
    {data && <><p>Severity: {data.severity ?? "N/A"}. Response deadline: {date(data.responseDeadlineAt)}. Resolution deadline: {date(data.resolutionDeadlineAt)}.</p>
      <p>{data.timing.classification} — time used: {data.timing.percentUsed == null ? "N/A" : `${data.timing.percentUsed.toFixed(1)}%`}; at-risk threshold: 80%. Server snapshot: {date(data.serverNow)}.</p>
      {data.timing.unavailableReason && <p>{data.timing.unavailableReason}</p>}
      {data.items.map(item => <article key={item.escalationId}><h4>Escalation #{item.escalationId} — {item.state}</h4>
        <p>Opened: {date(item.openedAt)}{item.closedAt && `; Closed: ${date(item.closedAt)}`}</p><p>Reason: {item.reason}</p><p>Requested support: {item.requestedSupport}</p></article>)}
      {data.canCreate && <><fieldset disabled={busy || attempted || review}>
        <label>Reason (10–4000 characters)<textarea maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></label>
        <label>Requested support<textarea maxLength={4000} value={support} onChange={e => setSupport(e.target.value)} /></label>
      </fieldset>{review ? <><p>Create escalation and request Operations Manager acknowledgment?</p><button disabled={busy} onClick={() => void submit()}>Confirm escalation</button><button disabled={busy} onClick={() => setReview(false)}>Back</button></>
        : <button disabled={!valid || busy || attempted} onClick={() => setReview(true)}>Review escalation</button>}</>}
    </>}
  </section>;
}
