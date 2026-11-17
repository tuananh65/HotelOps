import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Workspace = {
  warehouses: { id: number; name: string; isActive: boolean; canWrite: boolean }[];
  items: { id: number; name: string; baseUnitId: number }[];
  units: { id: number; name: string }[]; conversions: { itemId: number; unitId: number }[];
  documents: { documentId: number; documentNo: string; status: string; sourceWarehouseId: number; destinationWarehouseId: number; documentDate: string }[];
  count: number; page: number; pageSize: number;
};
type Entry = { itemId: string; uomId: string; expectedQty: string };
export function TransferWorkspacePage() {
  const { state, actions } = useAuth(); const navigate = useNavigate();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const [page, setPage] = useState(1), [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{ owner: typeof owner; page: number; data: Workspace }>();
  const data = loaded?.owner === owner && loaded.page === page ? loaded.data : null;
  const [source, setSource] = useState(""), [destination, setDestination] = useState("");
  const [date, setDate] = useState(""), [note, setNote] = useState("");
  const [lines, setLines] = useState<Entry[]>([{ itemId: "", uomId: "", expectedQty: "" }]);
  const [message, setMessage] = useState(""), [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false);
  const writer = useRef<AbortController | null>(null);
  useEffect(() => { setSource(""); setDestination(""); setDate(""); setNote(""); setLines([{ itemId: "", uomId: "", expectedQty: "" }]); }, [owner]);
  useEffect(() => {
    setLoaded(undefined); if (!owner) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/transfer-workspace?page=${page}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        if (response.status === 401) actions.invalidateSession(owner);
        const value = await response.json();
        if (!response.ok) throw new Error(value.detail ?? `Transfer workspace unavailable (${response.status}).`);
        if (!abort.signal.aborted && current.current === owner) setLoaded({ owner, page, data: value });
      } catch (e) { if (!abort.signal.aborted) setMessage(e instanceof Error ? e.message : "Workspace unavailable."); }
    })();
    const refresh = () => { if (!writer.current) { setLoaded(undefined); setRevision(v => v + 1); } };
    const timer = setInterval(refresh, 30000); window.addEventListener("focus", refresh);
    return () => { abort.abort(); clearInterval(timer); window.removeEventListener("focus", refresh); };
  }, [owner, page, revision, actions]);
  useEffect(() => () => writer.current?.abort(), [owner]);
  async function create() {
    if (!owner || !data || busy || uncertain || writer.current) return;
    const eligible = data.warehouses.filter(w => w.isActive && w.canWrite);
    if (source === destination || ![source, destination].every(id => eligible.some(w => String(w.id) === id))) { setMessage("Select two different authorized warehouses."); return; }
    if (!date || !lines.length || lines.some(l => !/^\d+(\.\d{1,3})?$/.test(l.expectedQty) || !/[1-9]/.test(l.expectedQty)
      || !data.items.some(i => String(i.id) === l.itemId) || !data.units.some(u => String(u.id) === l.uomId))) { setMessage("Select items/units and positive quantities."); return; }
    const abort = new AbortController(); writer.current = abort; setBusy(true); let sent = false;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      const csrf = await bootstrap.json();
      if (!bootstrap.ok || csrf.headerName !== "X-CSRF-TOKEN" || !csrf.csrfToken) throw new Error("CSRF unavailable.");
      if (abort.signal.aborted || current.current !== owner) return;
      sent = true;
      const response = await fetch("/api/inventory/transfers", { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken }, body: JSON.stringify({ sourceWarehouseId: Number(source), destinationWarehouseId: Number(destination), documentDate: date, note,
          lines: lines.map(l => ({ itemId: Number(l.itemId), uomId: Number(l.uomId), expectedQty: l.expectedQty })) }) });
      if (response.status === 401) actions.invalidateSession(owner);
      const value = await response.json();
      if (!response.ok) { if (response.status < 500) sent = false; if ([401, 403].includes(response.status)) setLoaded(undefined); throw new Error(value.detail ?? `Create failed (${response.status}).`); }
      if (!abort.signal.aborted && current.current === owner) navigate(`/inventory/transfers/${value.documentId}`);
    } catch (e) { if (sent) setUncertain(true); if (!abort.signal.aborted) setMessage(`${e instanceof Error ? e.message : "Create failed."}${sent ? " Outcome unknown. Inspect the transfer list before creating again." : ""}`); }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  const update = (index: number, patch: Partial<Entry>) => setLines(rows => rows.map((row, i) => i === index ? { ...row, ...patch } : row));
  return <main><h1>Stock transfers</h1>{message && <p role="status">{message}</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload</button>
    {data && <><table><thead><tr><th>Transfer</th><th>Status</th><th>Source</th><th>Destination</th><th>Date</th></tr></thead><tbody>
      {data.documents.map(d => <tr key={d.documentId}><td><Link to={`/inventory/transfers/${d.documentId}`}>{d.documentNo}</Link></td><td>{d.status}</td>
        <td>{data.warehouses.find(w => w.id === d.sourceWarehouseId)?.name}</td><td>{data.warehouses.find(w => w.id === d.destinationWarehouseId)?.name}</td><td>{d.documentDate}</td></tr>)}</tbody></table>
      <p>{data.count} authorized transfers; page {data.page}</p><button disabled={busy || page === 1} onClick={() => setPage(p => p - 1)}>Previous</button><button disabled={busy || page * data.pageSize >= data.count} onClick={() => setPage(p => p + 1)}>Next</button>
      <h2>Create transfer</h2><form onSubmit={e => { e.preventDefault(); void create(); }}><fieldset disabled={busy || uncertain}>
        {(["source", "destination"] as const).map(side => <label key={side}>{side} warehouse<select required value={side === "source" ? source : destination} onChange={e => side === "source" ? setSource(e.target.value) : setDestination(e.target.value)}><option value="">Select warehouse</option>
          {data.warehouses.filter(w => w.isActive && w.canWrite).map(w => <option key={w.id} value={w.id}>{w.name}</option>)}</select></label>)}
        <label>Document date<input required type="date" value={date} onChange={e => setDate(e.target.value)} /></label><label>Note<textarea value={note} onChange={e => setNote(e.target.value)} /></label>
        {lines.map((line, index) => <fieldset key={index}><legend>Line {index + 1}</legend>
          <label>Material<select required value={line.itemId} onChange={e => { const item = data.items.find(i => String(i.id) === e.target.value); update(index, { itemId: e.target.value, uomId: item ? String(item.baseUnitId) : "" }); }}><option value="">Select material</option>{data.items.map(i => <option key={i.id} value={i.id}>{i.name}</option>)}</select></label>
          <label>Unit<select required value={line.uomId} onChange={e => update(index, { uomId: e.target.value })}><option value="">Select unit</option>{data.units.filter(u => data.items.some(i => String(i.id) === line.itemId && i.baseUnitId === u.id) || data.conversions.some(c => String(c.itemId) === line.itemId && c.unitId === u.id)).map(u => <option key={u.id} value={u.id}>{u.name}</option>)}</select></label>
          <label>Quantity<input required inputMode="decimal" pattern="[0-9]+([.][0-9]{1,3})?" value={line.expectedQty} onChange={e => update(index, { expectedQty: e.target.value })} /></label><button type="button" disabled={lines.length === 1} onClick={() => setLines(rows => rows.filter((_, i) => i !== index))}>Remove line</button>
        </fieldset>)}<button type="button" onClick={() => setLines(rows => [...rows, { itemId: "", uomId: "", expectedQty: "" }])}>Add line</button><button>Save draft</button>
      </fieldset></form></>}
  </main>;
}
