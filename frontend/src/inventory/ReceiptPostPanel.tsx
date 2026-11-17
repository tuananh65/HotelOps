import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Receipt = { documentId: number; documentNo: string; status: string; rowVersion: string;
  lines: { lineId: number; itemId: number; uomId: number }[] };
type Detail = { document: Receipt; source: { replenishmentId: number | null } | null; expectedQuantities: string[] };
type Entry = { lineId: number; acceptedQty: string; rejectedQty: string; damagedQty: string; discrepancyReason: string };

export function ReceiptPostPanel({ id, onSaved, items, units }: { id: number; onSaved: () => void; items: { id: number; name: string }[]; units: { id: number; name: string }[] }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const loadedVersion = useRef<{ owner: typeof owner; version: string } | null>(null);
  const [data, setData] = useState<{ owner: typeof owner; value: Detail } | null>(null);
  const [lines, setLines] = useState<Entry[]>([]), [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [revision, setRevision] = useState(0);
  const writer = useRef<AbortController | null>(null);
  const receipt = data?.owner === owner ? data.value : null;
  useEffect(() => {
    setData(null); if (!owner) { setLines([]); return; }
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/receipt-workspace/${id}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        const value = await response.json();
        if (response.status === 401) actions.invalidateSession(owner);
        if (!response.ok) throw new Error(value.detail ?? `Receipt unavailable (${response.status}).`);
        if (abort.signal.aborted || current.current !== owner) return;
        setData({ owner, value });
        if (loadedVersion.current?.owner !== owner || loadedVersion.current?.version !== value.document.rowVersion)
        setLines(value.document.lines.map((l: { lineId: number }, i: number) => ({ lineId: l.lineId, acceptedQty: value.expectedQuantities[i], rejectedQty: "0", damagedQty: "0", discrepancyReason: "" })));
        loadedVersion.current = { owner, version: value.document.rowVersion };
        if (value.document.status === "POSTED") setMessage("MSG28: Receipt already posted. No additional stock is posted.");
      } catch (e) { if (!abort.signal.aborted) setMessage(e instanceof Error ? e.message : "Receipt unavailable."); }
    })();
    // Revalidate current authority; preserve entered quantities only for the same source version.
    const refresh = () => { if (!writer.current) { setData(null); setRevision(v => v + 1); } };
    window.addEventListener("focus", refresh); const timer = window.setInterval(refresh, 30000);
    return () => { abort.abort(); clearInterval(timer); window.removeEventListener("focus", refresh); };
  }, [owner, id, revision, actions]);
  useEffect(() => () => writer.current?.abort(), [owner, id]);
  async function post() {
    if (!owner || !receipt || busy || uncertain || receipt.document.status !== "DRAFT" || receipt.source?.replenishmentId) return;
    if (!window.confirm("Post the reviewed accepted quantities? Posted stock cannot be edited.")) return;
    const abort = new AbortController(); writer.current = abort; setBusy(true); setMessage(""); let sent = false;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      const csrf = await bootstrap.json();
      if (!bootstrap.ok || csrf.headerName !== "X-CSRF-TOKEN" || !csrf.csrfToken) throw new Error("CSRF unavailable.");
      if (abort.signal.aborted || current.current !== owner) return;
      sent = true;
      const response = await fetch(`/api/inventory/receipts/${id}/post`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken }, body: JSON.stringify({ expectedRowVersion: receipt.document.rowVersion, lines }) });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) { if (response.status < 500) sent = false; if ([401, 403].includes(response.status)) setData(null); throw new Error((await response.json()).detail ?? `Posting failed (${response.status}).`); }
      if (current.current === owner && !abort.signal.aborted) { setMessage("MSG08: Receipt posted."); setRevision(v => v + 1); onSaved(); }
    } catch (e) { if (sent) setUncertain(true); if (!abort.signal.aborted) setMessage(`${e instanceof Error ? e.message : "Posting failed."}${sent ? " Outcome unknown. Reload the receipt before any further action." : ""}`); }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  return <section aria-label="Verify and post receipt"><h2>Verify and post</h2>{message && <p role="status">{message}</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload receipt</button>
    {receipt && <><p>{receipt.document.documentNo}: {receipt.document.status}</p>
      {receipt.source?.replenishmentId ? <p><Link to={`/operations/replenishments/${receipt.source.replenishmentId}`}>Verify and post through linked replenishment</Link> — stock and received quantities must commit together.</p>
      : receipt.document.status === "DRAFT" && <form onSubmit={e => { e.preventDefault(); void post(); }}><fieldset disabled={busy || uncertain}>
        <p>Accepted quantities increase stock; rejected and damaged quantities do not. Explain any difference from expected quantities.</p>
        {lines.map((line, index) => <fieldset key={line.lineId}><legend>{items.find(i => i.id === receipt.document.lines[index].itemId)?.name ?? `Item ${receipt.document.lines[index].itemId}`}, {units.find(u => u.id === receipt.document.lines[index].uomId)?.name ?? `unit ${receipt.document.lines[index].uomId}`}; expected {receipt.expectedQuantities[index]}</legend>
          {(["acceptedQty", "rejectedQty", "damagedQty"] as const).map(field => <label key={field}>{field}<input required inputMode="decimal" pattern="[0-9]+([.][0-9]{1,3})?" value={line[field]} onChange={e => setLines(v => v.map(x => x.lineId === line.lineId ? { ...x, [field]: e.target.value } : x))} /></label>)}
          <label>Discrepancy reason <input value={line.discrepancyReason} onChange={e => setLines(v => v.map(x => x.lineId === line.lineId ? { ...x, discrepancyReason: e.target.value } : x))} /></label>
        </fieldset>)}<button>Confirm and post</button></fieldset></form>}
    </>}
  </section>;
}
