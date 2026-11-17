import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Item = { kind: string; id: number; state: string; label: string; note: string | null };
type Receiver = { userId: number; name: string; assignmentId: number; start: string; end: string };
type Preview = { workdayId: number; branchId: number; departmentId: number; version: string; items: Item[]; receivers: Receiver[] };
type Receipt = { id: number; workdayId: number; branchId: number; departmentId: number; senderId: number; receiverId: number;
  previousId: number | null; state: string; version: string; submittedAt: string; decisionAt: string | null; reason: string | null;
  generalNote: string | null; items: Item[]; restrictedItems: number; canDecide: boolean };
type Summary = { id: number; workdayId: number; senderId: number; receiverId: number; state: string; submittedAt: string };
function sourcePath(item: Item, branch: number): string | null {
  switch (item.kind) {
    case "TASK": return `/operational-tasks?branchId=${branch}&taskId=${item.id}`;
    case "CLEANING": return `/cleaning/tasks/${item.id}?branchId=${branch}`;
    case "REWORK": return `/cleaning/reworks/${item.id}?branchId=${branch}`;
    case "INCIDENT": return `/operations/incidents/${item.id}`;
    case "INTERNAL_REQUEST": return `/internal-requests?requestId=${item.id}`;
    case "ROOM": return `/rooms/${item.id}?branchId=${branch}`;
    default: return null;
  }
}
const validId = (id: number) => Number.isSafeInteger(id) && id > 0;
const identities = new WeakMap<object, number>(); let serial = 0;
function identityKey(identity: object) { if (!identities.has(identity)) identities.set(identity, ++serial); return identities.get(identity)!; }

export function ShiftHandoverPage() {
  const { state, actions } = useAuth(); const { id } = useParams(); const [query] = useSearchParams();
  const identity = useMemo(() => ({}), [state.session, state.status, state.pending, id, query.toString()]);
  const current = useRef(identity); current.current = identity;
  if (state.status !== "AUTHENTICATED" || state.pending !== null || !state.session || state.session.mustChangePassword)
    return <p role="status">Confirm an unrestricted session to use shift handover.</p>;
  return <Bound key={identityKey(identity)} id={Number(id)} branch={Number(query.get("branchId"))} day={Number(query.get("workdayId"))}
    previous={Number(query.get("previousId"))} user={Number(state.session.user.id)} current={() => current.current === identity}
    recover={() => void actions.revalidate()} />;
}
function Bound({ id, branch, day, previous, user, current, recover }: { id: number; branch: number; day: number; previous: number; user: number; current: () => boolean; recover: () => void }) {
  const [preview, setPreview] = useState<Preview | null>(null), [receipt, setReceipt] = useState<Receipt | null>(null);
  const [rows, setRows] = useState<Summary[]>([]), [page, setPage] = useState(1), [reload, setReload] = useState(0);
  const [receiver, setReceiver] = useState(""), [general, setGeneral] = useState(""), [reason, setReason] = useState("");
  const [notes, setNotes] = useState<Record<string, string>>({}), [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false), [confirmed, setConfirmed] = useState(false), [unknown, setUnknown] = useState(false);
  const requestKey = useRef(crypto.randomUUID()), pending = useRef(false), controller = useRef<AbortController | null>(null);
  async function read(url: string, signal: AbortSignal) {
    const response = await fetch(url, { credentials: "include", cache: "no-store", redirect: "error", signal, headers: { Accept: "application/json" } });
    if (!response.ok) {
      if ((response.status === 401 || response.status === 403) && current()) recover();
      throw new Error(`Access or data unavailable (${response.status}).`);
    }
    return response.json();
  }
  useEffect(() => {
    const abort = new AbortController(); controller.current = abort; setBusy(true); setConfirmed(false);
    void (async () => {
      try {
        if (validId(id)) {
          const data = await read(`/api/shift-handovers/${id}`, abort.signal) as Receipt;
          if (data.id !== id || !Array.isArray(data.items) || typeof data.version !== "string") throw new Error("Invalid receipt response.");
          if (!abort.signal.aborted && current()) setReceipt(data);
        } else if (validId(day)) {
          const data = await read(`/api/shift-handovers/preview/${day}`, abort.signal) as Preview;
          if (data.workdayId !== day || !Array.isArray(data.items) || !Array.isArray(data.receivers) || typeof data.version !== "string") throw new Error("Invalid snapshot response.");
          if (!abort.signal.aborted && current()) { setPreview(data); setNotes({}); setReceiver(""); requestKey.current = crypto.randomUUID(); }
        } else if (validId(branch)) {
          const data = await read(`/api/shift-handovers?branchId=${branch}&page=${page}`, abort.signal) as Summary[];
          if (!Array.isArray(data)) throw new Error("Invalid handover list.");
          if (!abort.signal.aborted && current()) setRows(data);
        } else setMessage("Open Shift Handover from your current workday or choose a branch.");
      } catch (error) { if (!abort.signal.aborted && current()) setMessage(error instanceof Error ? error.message : "Read unavailable."); }
      finally { if (!abort.signal.aborted && current()) setBusy(false); }
    })();
    return () => abort.abort();
  }, [reload, page]);
  async function save(acknowledge?: boolean) {
    if (busy || pending.current || unknown || !confirmed || !controller.current) return;
    const signal = controller.current.signal; pending.current = true; setBusy(true); let dispatched = false;
    try {
      const csrf = await read("/api/auth/csrf", signal) as { headerName: string; csrfToken: string };
      if (csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !csrf.csrfToken || !current() || signal.aborted) throw new Error("CSRF/session unavailable.");
      const body = preview ? { requestKey: requestKey.current, receiverAssignmentId: Number(receiver), previousId: validId(previous) ? previous : null,
        generalNote: general.trim() || null, notes: preview.items.map(i => ({ kind: i.kind, id: i.id, note: notes[`${i.kind}:${i.id}`]?.trim() || null })) }
        : { acknowledge, reason: acknowledge ? null : reason.trim() };
      const version = preview?.version ?? receipt?.version;
      if (!version) throw new Error("Reload the current version.");
      dispatched = true;
      const response = await fetch(preview ? `/api/shift-handovers/workdays/${day}` : `/api/shift-handovers/${id}/decision`, {
        method: "POST", credentials: "include", cache: "no-store", redirect: "error", signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": version }, body: JSON.stringify(body),
      });
      if (!current() || signal.aborted) return;
      if (!response.ok) {
        if ([400,401,403,404,409,412,428].includes(response.status)) { dispatched = false; setPreview(null); setReceipt(null); }
        if (response.status === 401 || response.status === 403) recover();
        throw new Error(`Request not confirmed (${response.status}). Reload before continuing.`);
      }
      const result = await response.json() as { id: number; message: string };
      if (!validId(result.id) || result.message !== "MSG08") throw new Error("Invalid save receipt.");
      setMessage(`MSG08: Saved handover #${result.id}.`); setConfirmed(false);
      const saved = await read(`/api/shift-handovers/${result.id}`, signal) as Receipt;
      if (current() && !signal.aborted) { setPreview(null); setReceipt(saved); }
    } catch (error) {
      if (current() && !signal.aborted) {
        setUnknown(dispatched); setMessage(dispatched ? "Outcome not fully confirmed. Check handover history before making another change." : error instanceof Error ? error.message : "Save unavailable.");
      }
    } finally { pending.current = false; if (current() && !signal.aborted) setBusy(false); }
  }
  const selectedBranch = receipt?.branchId ?? preview?.branchId ?? branch;
  return <main><h1>Operational shift handover</h1>
    <p>Acknowledgment confirms awareness. Use the normal assignment workflows to change who performs any item.</p>
    <Link to="/workday">My workday</Link>{validId(selectedBranch) && <> · <Link to={`/workday/handovers?branchId=${selectedBranch}`}>Handover history</Link></>}
    {message && <p role="status">{message}</p>}{busy && <p role="status">Loading or saving…</p>}
    <button disabled={busy || unknown} onClick={() => { setMessage(""); setReload(n => n + 1); }}>Reload from server</button>
    {!validId(id) && !validId(day) && <><form method="get"><label>Branch ID<input name="branchId" type="number" min="1" defaultValue={branch || ""}/></label><button>Load history</button></form>
      <ul>{rows.map(r => <li key={r.id}><Link to={`/workday/handovers/${r.id}`}>#{r.id}</Link> — {r.state} — {r.submittedAt}</li>)}</ul>
      <button disabled={busy || page === 1} onClick={() => setPage(p => p - 1)}>Previous</button><span> Page {page} </span><button disabled={busy || rows.length < 25} onClick={() => setPage(p => p + 1)}>Next</button></>}
    {preview && <section><h2>Review snapshot for workday #{preview.workdayId}</h2>
      <label>Receiver on the next shift<select disabled={busy || unknown} value={receiver} onChange={e => { setReceiver(e.target.value); setConfirmed(false); }}><option value="">Choose receiver</option>
        {preview.receivers.map(r => <option key={r.assignmentId} value={r.assignmentId}>{r.name} — {r.start} to {r.end}</option>)}</select></label>
      {!preview.receivers.length && <p>No eligible receiver: current membership, next shift, VIEW and DECIDE grants are required.</p>}
      <ul>{preview.items.map(i => <li key={`${i.kind}:${i.id}`}>{i.kind} #{i.id}: {i.label} — {i.state}
        <label>Item note<textarea maxLength={4000} disabled={busy || unknown} value={notes[`${i.kind}:${i.id}`] ?? ""} onChange={e => { setNotes(n => ({ ...n, [`${i.kind}:${i.id}`]: e.target.value })); setConfirmed(false); }}/></label></li>)}</ul>
      <label>General notes<textarea maxLength={4000} value={general} disabled={busy || unknown} onChange={e => { setGeneral(e.target.value); setConfirmed(false); }}/></label>
      <label><input type="checkbox" checked={confirmed} disabled={busy || unknown} onChange={e => setConfirmed(e.target.checked)}/>Confirm this snapshot and receiver</label>
      <button disabled={busy || unknown || !confirmed || !receiver} onClick={() => void save()}>Submit handover</button></section>}
    {receipt && <section><h2>Handover #{receipt.id} — {receipt.state}</h2><p>Submitted: {receipt.submittedAt}; decision: {receipt.decisionAt ?? "pending"}</p>
      {receipt.previousId && <Link to={`/workday/handovers/${receipt.previousId}`}>Previous version</Link>}
      <p>{receipt.generalNote}</p><ul>{receipt.items.map(i => <li key={`${i.kind}:${i.id}`}>{i.kind} #{i.id}: {i.label} — {i.state}<p>{i.note}</p>{sourcePath(i, receipt.branchId) && <Link to={sourcePath(i, receipt.branchId)!}>Open source workflow</Link>}</li>)}</ul>
      {receipt.restrictedItems > 0 && <p>{receipt.restrictedItems} source items are hidden because current source read access is unavailable.</p>}
      {receipt.reason && <p>Rejection reason: {receipt.reason}</p>}
      {receipt.canDecide && <><label>Rejection reason (at least 10 characters)<textarea maxLength={4000} value={reason} disabled={busy || unknown} onChange={e => { setReason(e.target.value); setConfirmed(false); }}/></label>
        <label><input type="checkbox" checked={confirmed} disabled={busy || unknown} onChange={e => setConfirmed(e.target.checked)}/>Confirm decision</label>
        <button disabled={busy || unknown || !confirmed} onClick={() => void save(true)}>Acknowledge</button><button disabled={busy || unknown || !confirmed || reason.trim().length < 10} onClick={() => void save(false)}>Reject</button></>}
      {receipt.state === "REJECTED" && receipt.senderId === user && <Link to={`/workday/handovers?workdayId=${receipt.workdayId}&previousId=${receipt.id}`}>Prepare a new version (Working workday required)</Link>}
    </section>}
  </main>;
}
