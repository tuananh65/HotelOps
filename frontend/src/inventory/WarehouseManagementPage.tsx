import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";

type Warehouse = { warehouseId: number; warehouseCode: string; warehouseName: string; branchId: number; departmentId: number | null; isActive: boolean;
  managerUserId: number | null; locationDescription: string | null; rowVersion: string };
type Lookup = { id: number; code: string; name: string; isActive: boolean };
type Snapshot = { warehouses: { items: Warehouse[]; totalCount: number; page: number; pageSize: number }; branches: Lookup[]; departments: { id: number; branchId: number; name: string; isActive: boolean }[] };
type Blocker = { kind: string; id: number; detail: string };
type Edit = { item: Warehouse; wasActive: boolean };

export function WarehouseManagementPage() {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const [params, setParams] = useSearchParams();
  const page = params.get("page") ?? "1";
  const [loaded, setLoaded] = useState<{ owner: AuthorityGeneration; page: string; data: Snapshot } | null>(null);
  const [edit, setEdit] = useState<Edit | null>(null), [notice, setNotice] = useState("");
  const [revision, setRevision] = useState(0), [busy, setBusy] = useState(false);
  const [blockers, setBlockers] = useState<Blocker[]>([]);
  const writer = useRef<AbortController | null>(null);
  const data = owner && loaded?.owner === owner && loaded.page === page ? loaded.data : null;
  useEffect(() => { setEdit(null); setBlockers([]); setNotice(""); writer.current?.abort(); setBusy(false); }, [owner, page]);
  useEffect(() => {
    setLoaded(null);
    if (!owner) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/warehouse-management?page=${encodeURIComponent(page)}&pageSize=25`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        if (response.status === 401) actions.invalidateSession(owner);
        if (response.status === 403) { setLoaded(null); setEdit(null); setBlockers([]); }
        if (!response.ok) { const body = await response.json().catch(() => null); throw new Error(body?.detail ?? `Catalogue unavailable (${response.status}).`); }
        const body = await response.json() as Snapshot;
        if (body.warehouses?.page !== Number(page) || !Array.isArray(body.warehouses.items) || !Array.isArray(body.branches) || !Array.isArray(body.departments)) throw new Error("Invalid catalogue response.");
        if (!abort.signal.aborted && current.current === owner) setLoaded({ owner, page, data: body });
      } catch (e) { if (!abort.signal.aborted) { setEdit(null); setBlockers([]); setNotice(e instanceof Error ? e.message : "Catalogue unavailable."); } }
    })();
    const refresh = () => { setLoaded(null); setRevision(v => v + 1); };
    const timer = window.setInterval(refresh, 30000); window.addEventListener("focus", refresh);
    return () => { abort.abort(); window.clearInterval(timer); window.removeEventListener("focus", refresh); };
  }, [owner, page, revision, actions]);
  useEffect(() => () => writer.current?.abort(), []);
  async function save(value: Edit) {
    if (!owner || !data || busy) return;
    const item = value.item;
    const abort = new AbortController(); writer.current = abort; setBusy(true); setNotice("");
    try {
      setBlockers([]);
      if (item.warehouseId > 0 && value.wasActive && !item.isActive) {
        const preview = await fetch(`/api/warehouse-management/${item.warehouseId}/deactivation`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        if (preview.status === 401) actions.invalidateSession(owner);
        if (preview.status === 403) { setLoaded(null); setEdit(null); setBlockers([]); }
        const result = await preview.json();
        if (!preview.ok) throw new Error(result?.detail ?? "Cannot inspect warehouse blockers.");
        if (current.current !== owner || abort.signal.aborted) return;
        if (!Array.isArray(result.blockers)) throw new Error("Invalid blocker response.");
        if (result.blockers.length) { setBlockers(result.blockers); setNotice("MSG34: Resolve these blockers before deactivating the warehouse."); return; }
        if (result.rowVersion !== item.rowVersion) throw new Error("MSG03: Warehouse changed. Reload before retrying.");
        if (!window.confirm("MSG33: Deactivate this warehouse? Historical records will remain.")) return;
      }
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      const token = await bootstrap.json();
      if (!bootstrap.ok || token.headerName !== "X-CSRF-TOKEN" || typeof token.csrfToken !== "string" || !token.csrfToken) throw new Error("CSRF bootstrap unavailable.");
      if (current.current !== owner || abort.signal.aborted) return;
      const response = await fetch(`/api/inventory/warehouses${item.warehouseId ? `/${item.warehouseId}` : ""}`, { method: item.warehouseId ? "PUT" : "POST",
        credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal, headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token.csrfToken },
        body: JSON.stringify({ warehouseCode: item.warehouseCode, warehouseName: item.warehouseName, branchId: item.branchId, departmentId: item.departmentId,
          isActive: item.isActive, managerUserId: item.managerUserId, locationDescription: item.locationDescription,
          expectedRowVersion: item.warehouseId ? item.rowVersion : null }) });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) { const body = await response.json().catch(() => null); throw new Error(body?.detail ?? `Save failed (${response.status}).`); }
      if (!abort.signal.aborted && current.current === owner) { setEdit(null); setNotice("MSG08: Warehouse saved. Historical records remain unchanged."); setRevision(v => v + 1); }
    } catch (e) { if (!abort.signal.aborted && current.current === owner) setNotice(e instanceof Error ? e.message : "Save outcome unknown. Reload before retrying."); }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  const field = (value: Partial<Warehouse>) => { if (edit) setEdit({ ...edit, item: { ...edit.item, ...value } }); };
  return <section><h1>Manage warehouses</h1><Link to="/inventory/items">Inventory</Link>
    {notice && <p role="status">{notice}</p>}
    {data && <>
      {blockers.length > 0 && <table><thead><tr><th>Blocker</th><th>Record</th><th>Quantity / status</th></tr></thead><tbody>{blockers.map(b => <tr key={b.kind + b.id}><td>{b.kind}</td><td>{b.id}</td><td>{b.detail}</td></tr>)}</tbody></table>}
      <button disabled={busy} onClick={() => { setBlockers([]); setEdit({ wasActive: false, item: { warehouseId: 0, warehouseCode: "", warehouseName: "", branchId: 0, departmentId: null, isActive: true, managerUserId: null, locationDescription: null, rowVersion: "" } }); }}>Create warehouse</button>
      <button disabled={busy} onClick={() => { setEdit(null); setBlockers([]); setRevision(v => v + 1); }}>Reload/discard edits</button>
      <table><thead><tr><th>Code</th><th>Name</th><th>Branch</th><th>Department</th><th>Status</th><th>Actions</th></tr></thead><tbody>{data.warehouses.items.map(w => <tr key={w.warehouseId}>
        <td>{w.warehouseCode}</td><td>{w.warehouseName}</td><td>{data.branches.find(b => b.id === w.branchId)?.name ?? w.branchId}</td><td>{data.departments.find(d => d.id === w.departmentId)?.name ?? "Unassigned"}</td><td>{w.isActive ? "Active" : "Inactive"}</td>
        <td><button disabled={busy} onClick={() => { setBlockers([]); setEdit({ item: { ...w }, wasActive: w.isActive }); }}>Edit</button>
        {w.isActive && <button disabled={busy} onClick={() => void save({ item: { ...w, isActive: false }, wasActive: true })}>Deactivate</button>}</td></tr>)}</tbody></table>
      {data.warehouses.items.length === 0 && <p>MSG10: No warehouses.</p>}
      <button disabled={busy || !!edit || data.warehouses.page <= 1} onClick={() => setParams({ page: String(data.warehouses.page - 1) })}>Previous</button>
      <button disabled={busy || !!edit || data.warehouses.page * data.warehouses.pageSize >= data.warehouses.totalCount} onClick={() => setParams({ page: String(data.warehouses.page + 1) })}>Next</button>
      {edit && <form onSubmit={e => { e.preventDefault(); void save(edit); }}><fieldset disabled={busy}>
        <label>Code <input required maxLength={40} value={edit.item.warehouseCode} onChange={e => field({ warehouseCode: e.target.value })} /></label>
        <label>Name <input required maxLength={150} value={edit.item.warehouseName} onChange={e => field({ warehouseName: e.target.value })} /></label>
        <label>Branch <select required value={edit.item.branchId || ""} onChange={e => field({ branchId: Number(e.target.value), departmentId: null })}><option value="">Select branch</option>
          {data.branches.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select></label>
        <label>Responsible department <select required value={edit.item.departmentId ?? ""} onChange={e => field({ departmentId: Number(e.target.value) })}><option value="">Select department</option>
          {data.departments.filter(d => d.branchId === edit.item.branchId && (d.isActive || d.id === edit.item.departmentId)).map(d => <option key={d.id} value={d.id}>{d.name}{d.isActive ? "" : " (inactive)"}</option>)}</select></label>
        <label>Address / description <textarea value={edit.item.locationDescription ?? ""} onChange={e => field({ locationDescription: e.target.value || null })} /></label>
        <label><input type="checkbox" checked={edit.item.isActive} onChange={e => field({ isActive: e.target.checked })} />Active</label><button>Save warehouse</button>
      </fieldset></form>}
    </>}
  </section>;
}
