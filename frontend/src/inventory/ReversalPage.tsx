import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Preview = { documentId: number; documentNo: string; documentType: string; status: string; rowVersion: string; canReverse: boolean; blocker: string | null;
  effects: { warehouseId: number; itemId: number; currentQty: string; reversalQty: string; resultingQty: string }[];
  reversals: { documentId: number; documentNo: string; status: string }[] };
export function ReversalPage() {
  const { id = "" } = useParams(); const navigate = useNavigate(); const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const [loaded, setLoaded] = useState<{ owner: typeof owner; id: string; value: Preview }>();
  const [revision, setRevision] = useState(0), [reason, setReason] = useState("");
  const [message, setMessage] = useState(""), [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false);
  const writer = useRef<AbortController | null>(null);
  const data = loaded?.owner === owner && loaded.id === id ? loaded.value : null;
  useEffect(() => { setReason(""); }, [owner, id]);
  useEffect(() => {
    setLoaded(undefined); if (!owner || !/^[1-9]\d*$/.test(id) || !Number.isSafeInteger(Number(id))) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/stocktake-workspace/reversal/${id}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        if (response.status === 401) actions.invalidateSession(owner);
        const value = await response.json();
        if (!response.ok) throw new Error(value.detail ?? `Reversal access denied or unavailable (${response.status}).`);
        if (!abort.signal.aborted && current.current === owner) setLoaded({ owner, id, value });
      } catch (e) { if (!abort.signal.aborted) setMessage(e instanceof Error ? e.message : "Reversal unavailable."); }
    })();
    const refresh = () => { if (!writer.current) { setLoaded(undefined); setRevision(v => v + 1); } };
    const timer = setInterval(refresh, 30000); window.addEventListener("focus", refresh);
    return () => { abort.abort(); clearInterval(timer); window.removeEventListener("focus", refresh); };
  }, [owner, id, revision, actions]);
  useEffect(() => () => writer.current?.abort(), [owner, id]);
  async function reverse() {
    if (!owner || !data?.canReverse || busy || uncertain || writer.current) return;
    if (reason.trim().length < 10) { setMessage("MSG03: Enter a reason of at least 10 characters."); return; }
    if (!window.confirm(`MSG33: Reverse ${data.documentNo}? The original will remain in the ledger with an offsetting document.`)) return;
    const abort = new AbortController(); writer.current = abort; setBusy(true); let sent = false;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      const csrf = await bootstrap.json();
      if (!bootstrap.ok || csrf.headerName !== "X-CSRF-TOKEN" || !csrf.csrfToken) throw new Error("CSRF unavailable.");
      if (abort.signal.aborted || current.current !== owner) return;
      sent = true;
      const response = await fetch(`/api/inventory/documents/${id}/reverse`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken }, body: JSON.stringify({ expectedRowVersion: data.rowVersion, reason }) });
      if (response.status === 401) actions.invalidateSession(owner);
      const value = await response.json();
      if (!response.ok) { if (response.status < 500) sent = false; setLoaded(undefined); throw new Error(value.detail ?? `Reversal failed (${response.status}).`); }
      if (!abort.signal.aborted && current.current === owner) { setMessage(`MSG08: Reversal ${value.documentNo} posted.`); setLoaded(undefined); setRevision(v => v + 1); }
    } catch (e) { if (sent) setUncertain(true); if (!abort.signal.aborted) setMessage(`${e instanceof Error ? e.message : "Reversal failed."}${sent ? " Outcome unknown. Inspect the original and reversal before retrying." : ""}`); }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  return <main><h1>Reverse stock transaction</h1><button onClick={() => navigate(`/inventory/documents/${id}`)}>Original document</button> | <Link to="/inventory/stock">Stock ledger</Link>
    {message && <p role="status">{message}</p>}<button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload authority and balances</button>
    {data && <><h2>{data.documentNo} — {data.status}</h2><p>{data.documentType}. The server rechecks dependencies, current scope and stock at confirmation.</p>
      {data.blocker && <p role="alert">{data.blocker}</p>}
      <table><thead><tr><th>Warehouse</th><th>Item</th><th>Current base quantity</th><th>Reversal</th><th>Result</th></tr></thead><tbody>{data.effects.map(l => <tr key={`${l.warehouseId}:${l.itemId}`}><td>{l.warehouseId}</td><td>{l.itemId}</td><td>{l.currentQty}</td><td>{l.reversalQty}</td><td>{l.resultingQty}</td></tr>)}</tbody></table>
      {data.reversals.map(d => <p key={d.documentId}><Link to={`/inventory/documents/${d.documentId}`}>{d.documentNo}</Link>: {d.status}</p>)}
      {data.canReverse && <form onSubmit={e => { e.preventDefault(); void reverse(); }}><fieldset disabled={busy || uncertain}><label>Reversal reason<textarea required minLength={10} value={reason} onChange={e => setReason(e.target.value)} /></label><button>Confirm reversal</button></fieldset></form>}
    </>}
  </main>;
}
