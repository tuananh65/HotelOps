import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Issue = { documentId: number; documentNo: string; warehouseId: number; lines: {
  lineId: number; itemId: number; uomId: number; itemName: string; uomName: string;
  issued: string; returned: string; consumed: string; remaining: string }[] };
type Detail = { document: { documentId: number; documentNo: string; status: string; rowVersion: string; reason: string;
  lines: { lineId: number; itemId: number; uomId: number; requestedQty: number; actualQty: number; damagedQty: number | null; discrepancyReason: string | null }[] };
  canWithdraw?: boolean; canApprove?: boolean; approvalValid: boolean; approvedAt: string | null };

export function ReturnRequestPage({ approve = false }: { approve?: boolean }) {
  const { id, issueId } = useParams(), navigate = useNavigate();
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const [snapshot, setSnapshot] = useState<{ owner: typeof owner; key: string; issue?: Issue; detail?: Detail } | null>(null);
  const [quantities, setQuantities] = useState<Record<number, string>>({}), [reason, setReason] = useState("");
  const [message, setMessage] = useState(""), [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [revision, setRevision] = useState(0);
  const writer = useRef<AbortController | null>(null);
  const key = issueId ? `issue:${issueId}` : `${approve ? "approve" : "own"}:${id}`;
  const validId = /^[1-9]\d*$/.test(issueId ?? id ?? "") && Number.isSafeInteger(Number(issueId ?? id));
  const data = snapshot?.owner === owner && snapshot.key === key ? snapshot : null;
  const url = issueId ? `/api/return-requests/issues/${issueId}` : `/api/${approve ? "return-approvals" : "return-requests"}/${id}`;
  useEffect(() => {
    setSnapshot(null);
    if (!owner || !validId) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(url, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        const value = await response.json();
        if (response.status === 401) actions.invalidateSession(owner);
        if (!response.ok) throw new Error(value.detail ?? `Read failed (${response.status}).`);
        if (!abort.signal.aborted && current.current === owner) {
          setSnapshot({ owner, key, ...(issueId ? { issue: value } : { detail: value }) });
          if (!issueId) setUncertain(false);
        }
      } catch (e) { if (!abort.signal.aborted) setMessage(e instanceof Error ? e.message : "Source unavailable."); }
    })();
    const refresh = () => { if (!writer.current) { setSnapshot(null); setRevision(v => v + 1); } };
    const timer = window.setInterval(refresh, 30000); window.addEventListener("focus", refresh);
    return () => { abort.abort(); window.clearInterval(timer); window.removeEventListener("focus", refresh); };
  }, [owner, key, url, validId, issueId, revision, actions]);
  useEffect(() => { setQuantities({}); setReason(""); return () => writer.current?.abort(); }, [owner, key]);

  async function command() {
    if (!owner || !data || busy || uncertain) return;
    const issue = data.issue, detail = data.detail;
    if (!issue && !(approve ? detail?.canApprove : detail?.canWithdraw)) return;
    let body: unknown;
    if (issue) {
      const lines = issue.lines.filter(l => quantities[l.lineId] && quantities[l.lineId] !== "0")
        .map(l => ({ originalIssueLineId: l.lineId, itemId: l.itemId, uomId: l.uomId, expectedQty: quantities[l.lineId] }));
      if (!lines.length || !reason.trim()) { setMessage("Choose return quantities and enter a reason."); return; }
      body = { destinationWarehouseId: issue.warehouseId, documentDate: new Date().toISOString().slice(0, 10), reason: reason.trim(), lines };
    } else body = { expectedRowVersion: detail!.document.rowVersion, ...(approve ? {} : { reason: reason.trim() }) };
    if (!window.confirm(issue ? "Submit this return request without changing stock?" : approve ? "Approve these declared quantities? Stock is posted separately." : "Withdraw this return request?")) return;
    const abort = new AbortController(); writer.current = abort; setBusy(true); setMessage(""); let sent = false;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      const csrf = await bootstrap.json();
      if (bootstrap.status === 401) actions.invalidateSession(owner);
      if (!bootstrap.ok || csrf.headerName !== "X-CSRF-TOKEN" || !csrf.csrfToken) throw new Error("CSRF unavailable.");
      if (abort.signal.aborted || current.current !== owner) return;
      sent = true;
      const response = await fetch(issue ? "/api/return-requests" : approve ? url : `${url}/withdraw`, {
        method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken }, body: JSON.stringify(body) });
      const value = await response.json();
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) { if (response.status < 500) sent = false; if ([401, 403].includes(response.status)) setSnapshot(null); throw new Error(value.detail ?? `Request failed (${response.status}).`); }
      if (current.current !== owner || abort.signal.aborted) return;
      if (issue) navigate(`/inventory/return-requests/${value.documentId}`);
      else { setMessage(approve ? "Return approved; awaiting warehouse inspection and posting." : "Return withdrawn. Stock unchanged."); setRevision(v => v + 1); }
    } catch (e) { if (sent) setUncertain(true); if (!abort.signal.aborted) setMessage(`${e instanceof Error ? e.message : "Request failed."}${sent ? " Outcome unknown. Check the request before submitting again." : ""}`); }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }

  return <section aria-label="Return materials"><h2>{issueId ? "Return unused materials" : approve ? "Approve return request" : "My return request"}</h2>
    <Link to="/inventory/return-requests">Return queues</Link>
    {message && <p role="status">{message}</p>}<button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload</button>
    {data?.issue && <form onSubmit={e => { e.preventDefault(); void command(); }}><fieldset disabled={busy || uncertain}>
      <p>Issue {data.issue.documentNo}. Only posted usable returns increase warehouse stock.</p>
      {data.issue.lines.map(l => <label key={l.lineId}>{l.itemName} ({l.uomName}): issued {l.issued}, already returned {l.returned}, consumed {l.consumed}, remaining {l.remaining}.
        <input aria-label={`Return ${l.itemName}`} inputMode="decimal" pattern="[0-9]+([.][0-9]{1,3})?" value={quantities[l.lineId] ?? ""} onChange={e => setQuantities(v => ({ ...v, [l.lineId]: e.target.value }))} /></label>)}
      <label>Reason<textarea required maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /></label><button>Submit return</button>
    </fieldset></form>}
    {data?.detail && <><p>{data.detail.document.documentNo}: {data.detail.document.status}. {data.detail.approvalValid ? "Independent approval recorded." : "No valid independent approval."}</p>
      <p>{data.detail.document.reason}</p><ul>{data.detail.document.lines.map(l => <li key={l.lineId}>Item {l.itemId}, unit {l.uomId}: declared {l.requestedQty}, received usable {l.actualQty}, damaged {l.damagedQty ?? "N/A"}. {l.discrepancyReason}</li>)}</ul>
      {(approve ? data.detail.canApprove : data.detail.canWithdraw) && <form onSubmit={e => { e.preventDefault(); void command(); }}><fieldset disabled={busy || uncertain}>
        {!approve && <label>Withdrawal reason<input required minLength={3} maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /></label>}
        <button>{approve ? "Approve declared return" : "Withdraw return"}</button></fieldset></form>}
      {approve && <Link to={`/inventory/returns/${id}`}>Open warehouse inspection (separate permission required)</Link>}
    </>}
  </section>;
}
