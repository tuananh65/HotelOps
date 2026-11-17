import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Receipt = { documentId: number; documentNo: string; status: string; rowVersion: string;
  lines: { lineId: number; itemId: number; uomId: number }[] };
type Detail = { canPost: boolean; approvalValid: boolean; document: Receipt; originals: { returnLineId: number; originalIssueLineId: number; originalDocumentNo: string; itemName: string; uomName: string }[]; expectedQuantities: string[]; receivedQuantities: { lineId: number; accepted: string; damaged: string | null; reason: string | null }[] };
type Entry = { lineId: number; acceptedQty: string; rejectedQty: string; damagedQty: string; discrepancyReason: string };

export function ReturnPostPage() {
  const { id: rawId = "" } = useParams();
  const id = /^[1-9]\d*$/.test(rawId) && Number.isSafeInteger(Number(rawId)) ? Number(rawId) : 0;
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const loadedVersion = useRef<{ owner: typeof owner; version: string } | null>(null);
  const [data, setData] = useState<{ owner: typeof owner; id: number; value: Detail } | null>(null);
  const [lines, setLines] = useState<Entry[]>([]), [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [revision, setRevision] = useState(0);
  const writer = useRef<AbortController | null>(null);
  const receipt = data?.owner === owner && data.id === id ? data.value : null;
  useEffect(() => {
    setData(null); if (!owner || !id) { setLines([]); return; }
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/return-workspace/${id}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        const value = await response.json();
        if (response.status === 401) actions.invalidateSession(owner);
        if (!response.ok) throw new Error(value.detail ?? `Return unavailable (${response.status}).`);
        if (abort.signal.aborted || current.current !== owner) return;
        setData({ owner, id, value });
        if (loadedVersion.current?.owner !== owner || loadedVersion.current?.version !== value.document.rowVersion)
        setLines(value.document.lines.map((l: { lineId: number }, i: number) => ({ lineId: l.lineId, acceptedQty: value.expectedQuantities[i], rejectedQty: "0", damagedQty: "0", discrepancyReason: "" })));
        loadedVersion.current = { owner, version: value.document.rowVersion };
        if (value.document.status === "POSTED") { setUncertain(false); setMessage("MSG28: Return already posted. No additional stock is posted."); }
      } catch (e) { if (!abort.signal.aborted) setMessage(e instanceof Error ? e.message : "Return unavailable."); }
    })();
    // Revalidate current authority; preserve entered quantities only for the same source version.
    const refresh = () => { if (!writer.current) { setData(null); setRevision(v => v + 1); } };
    window.addEventListener("focus", refresh); const timer = window.setInterval(refresh, 30000);
    return () => { abort.abort(); clearInterval(timer); window.removeEventListener("focus", refresh); };
  }, [owner, id, revision, actions]);
  useEffect(() => () => writer.current?.abort(), [owner, id]);
  async function post() {
    if (!owner || !receipt || busy || uncertain || receipt.document.status !== "DRAFT" || !receipt.canPost) return;
    if (!window.confirm("Post the reviewed accepted quantities? Posted stock cannot be edited.")) return;
    const abort = new AbortController(); writer.current = abort; setBusy(true); setMessage(""); let sent = false;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      const csrf = await bootstrap.json();
      if (!bootstrap.ok || csrf.headerName !== "X-CSRF-TOKEN" || !csrf.csrfToken) throw new Error("CSRF unavailable.");
      if (abort.signal.aborted || current.current !== owner) return;
      sent = true;
      const response = await fetch(`/api/inventory/returns/${id}/post`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken }, body: JSON.stringify({ expectedRowVersion: receipt.document.rowVersion, lines }) });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) { if (response.status < 500) sent = false; if ([401, 403].includes(response.status)) setData(null); throw new Error((await response.json()).detail ?? `Posting failed (${response.status}).`); }
      if (current.current === owner && !abort.signal.aborted) { setMessage("MSG08: Return posted."); setRevision(v => v + 1); }
    } catch (e) { if (sent) setUncertain(true); if (!abort.signal.aborted) setMessage(`${e instanceof Error ? e.message : "Posting failed."}${sent ? " Outcome unknown. Reload the return before any further action." : ""}`); }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  return <section aria-label="Inspect and post return"><h2>Verify and post</h2>{message && <p role="status">{message}</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload return</button>
    {receipt && <><p>{receipt.document.documentNo}: {receipt.document.status}</p>
      {receipt.document.status !== "DRAFT" && <section aria-label="Recorded return result">
        <p>These are recorded return quantities. Cancelled or reversed documents do not establish available stock.</p>
        <ul>{receipt.receivedQuantities.map(line => {
          const source = receipt.originals.find(o => o.returnLineId === line.lineId);
          return <li key={line.lineId}>{source?.itemName ?? String(line.lineId)}: usable {line.accepted}, damaged {line.damaged ?? "N/A"} {source?.uomName ?? ""}; original issue {source?.originalDocumentNo ?? "N/A"}.
            {line.reason && <p>{line.reason}</p>}</li>;
        })}</ul>
      </section>}
      {receipt.document.status === "DRAFT" && !receipt.canPost && <p>Independent approval is required before posting. <Link to={`/inventory/returns/${id}/approve`}>Review approval (separate permission required)</Link></p>}
      {receipt.document.status === "DRAFT" && <form onSubmit={e => { e.preventDefault(); void post(); }}><fieldset disabled={busy || uncertain || !receipt.canPost}>
        <p>Only usable accepted quantities increase stock. Damaged quantities are recorded separately. Explain damage or any shortage against the declared return.</p>
        {lines.map((line, index) => <fieldset key={line.lineId}><legend>{receipt.originals.find(o => o.returnLineId === line.lineId)?.itemName ?? `Item ${receipt.document.lines[index].itemId}`}, {receipt.originals.find(o => o.returnLineId === line.lineId)?.uomName ?? `unit ${receipt.document.lines[index].uomId}`}; declared {receipt.expectedQuantities[index]}; original issue {receipt.originals.find(o => o.returnLineId === line.lineId)?.originalDocumentNo ?? "unavailable"}, line {receipt.originals.find(o => o.returnLineId === line.lineId)?.originalIssueLineId ?? "unavailable"}</legend>
          {(["acceptedQty", "damagedQty"] as const).map(field => <label key={field}>{field}<input required inputMode="decimal" pattern="[0-9]+([.][0-9]{1,3})?" value={line[field]} onChange={e => setLines(v => v.map(x => x.lineId === line.lineId ? { ...x, [field]: e.target.value } : x))} /></label>)}
          <label>Discrepancy reason <input value={line.discrepancyReason} onChange={e => setLines(v => v.map(x => x.lineId === line.lineId ? { ...x, discrepancyReason: e.target.value } : x))} /></label>
        </fieldset>)}<button>Confirm and post</button></fieldset></form>}
    </>}
  </section>;
}
