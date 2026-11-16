import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

type Option = { id: number; code: string; name: string };
type Context = { incidentId: number; categoryId: number | null; severityId: number | null;
  affectsRoomReadiness: boolean | null; version: string; categories: Option[]; severities: Option[] };
const positive = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const options = (v: unknown): v is Option[] => Array.isArray(v) && v.every(x =>
  isObject(x) && positive(x.id) && typeof x.code === "string" && typeof x.name === "string");
const tag = (v: unknown): v is string => typeof v === "string" && /^"[0-9A-F]{16}"$/.test(v);

export function IncidentClassificationPanel({ data, onCommitted }: { data: Values; onCommitted?: (notice: string) => void }) {
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const mountedOwner = useRef(owner).current;
  const currentOwner = useRef(owner); currentOwner.current = owner;
  const id = data.incidentId;
  const [context, setContext] = useState<Context>();
  const [category, setCategory] = useState(""), [severity, setSeverity] = useState("");
  const [impact, setImpact] = useState("");
  const [message, setMessage] = useState(""), [busy, setBusy] = useState(false);
  const [attempted, setAttempted] = useState(false);
  const sending = useRef(false), writer = useRef<AbortController | null>(null);
  useEffect(() => {
    if (!owner || owner !== mountedOwner || !positive(id)) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/incidents/${id}/classification`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        if (response.status === 403 || response.status === 404 || response.status === 409) return;
        if (!response.ok) throw Error("Classification unavailable. Reload to check your session and configuration.");
        const raw: unknown = await response.json();
        if (!isObject(raw) || raw.incidentId !== id || !tag(raw.version)
          || !(raw.categoryId === null || positive(raw.categoryId)) || !(raw.severityId === null || positive(raw.severityId))
          || !(raw.affectsRoomReadiness === null || typeof raw.affectsRoomReadiness === "boolean")
          || !options(raw.categories) || !options(raw.severities)) throw Error("Classification details could not be confirmed.");
        if (!abort.signal.aborted && currentOwner.current === owner) {
          setContext(raw as Context);
          setCategory(raw.categoryId === null ? "" : String(raw.categoryId));
          setSeverity(raw.severityId === null ? "" : String(raw.severityId));
          setImpact(raw.affectsRoomReadiness === null ? "" : String(raw.affectsRoomReadiness));
        }
      } catch (e) { if (!abort.signal.aborted && currentOwner.current === owner) setMessage(e instanceof Error ? e.message : "Classification unavailable."); }
    })();
    return () => { abort.abort(); writer.current?.abort(); };
  }, [owner, mountedOwner, id]);
  const valid = context?.categories.some(x => String(x.id) === category) && context.severities.some(x => String(x.id) === severity)
    && (impact === "true" || impact === "false");
  async function save() {
    if (!owner || currentOwner.current !== owner || !context || !valid || sending.current || attempted) return;
    sending.current = true; setBusy(true); setAttempted(true);
    const abort = new AbortController(); writer.current = abort;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!bootstrap.ok) throw Error("Session check failed. Reload before retrying.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !/^[\x21-\x7e]+$/.test(csrf.csrfToken)
        || abort.signal.aborted || currentOwner.current !== owner) throw Error("Session changed. Reload.");
      const response = await fetch(`/api/incidents/${id}/classification`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": context.version },
        body: JSON.stringify({ categoryId: Number(category), severityId: Number(severity), affectsRoomReadiness: impact === "true" }) });
      if (!response.ok) throw Error(response.status === 412 ? "Incident changed. Reload before retrying."
        : "Classification was not confirmed. Reload to check the incident and current permissions.");
      const result: unknown = await response.json();
      if (!isObject(result) || result.incidentId !== id || !positive(result.eventId) || !tag(result.version) || typeof result.suggestRoomBlock !== "boolean")
        throw Error("Classification response could not be confirmed. Reload.");
      if (!abort.signal.aborted && currentOwner.current === owner) { const notice = result.suggestRoomBlock ? "Classification saved. Consider blocking the affected room using Block Room." : "Classification saved."; setMessage(notice); onCommitted?.(notice); }
    } catch (e) { if (!abort.signal.aborted && currentOwner.current === owner) setMessage(e instanceof Error ? e.message : "Classification not confirmed. Reload."); }
    finally { if (!abort.signal.aborted) { setBusy(false); sending.current = false; } }
  }
  if (!owner || owner !== mountedOwner || !positive(id)) return null;
  if (!context) return message ? <p role="status">{message}</p> : null;
  return <section aria-label="Classify incident"><h3>Classify incident</h3>
    <fieldset disabled={busy || attempted}>
      <label>Category<select value={category} onChange={e => setCategory(e.target.value)}><option value="">Select category</option>
        {context.categories.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <label>Severity<select value={severity} onChange={e => setSeverity(e.target.value)}><option value="">Select severity</option>
        {context.severities.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <label>Affects room readiness<select value={impact} onChange={e => setImpact(e.target.value)}>
        <option value="">Select impact</option><option value="true">Yes</option><option value="false">No</option></select></label>
    </fieldset>
    {context.severities.find(x => String(x.id) === severity)?.code === "CRITICAL" &&
      <p>Critical classification alerts eligible Maintenance Leads and Operations Managers and requires acknowledgment.</p>}
    {message && <p role="status">{message}</p>}
    <button disabled={!valid || busy || attempted} onClick={() => void save()}>Save classification</button>
  </section>;
}