import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";

type Unit = { uomId: number; uomCode: string; uomName: string; isActive: boolean; description: string | null; rowVersion: string };
type Conversion = { conversionId: number; itemId: number; fromUomId: number; toUomId: number; factor: string; isActive: boolean; rowVersion: string };
type Snapshot = { conversionPage: number; conversionPageSize: number; conversionTotalCount: number; units: { items: Unit[]; totalCount: number; page: number; pageSize: number }; choices: Unit[];
  item: { itemId: number; itemName: string; baseUomId: number; isActive: boolean } | null; conversions: Conversion[] };
type UnitForm = { id: number | null; code: string; name: string; description: string; active: boolean; version: string | null };
type ConversionForm = { id: number | null; from: string; factor: string; active: boolean; version: string | null };

export function UnitManagementPage() {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const [params, setParams] = useSearchParams();
  const query = params.toString();
  const [loaded, setLoaded] = useState<{ owner: AuthorityGeneration; query: string; data: Snapshot } | null>(null);
  const [unit, setUnit] = useState<UnitForm | null>(null), [conversion, setConversion] = useState<ConversionForm | null>(null);
  const [notice, setNotice] = useState("");
  const [revision, setRevision] = useState(0), [busy, setBusy] = useState(false);
  const writer = useRef<AbortController | null>(null);
  const data = owner && loaded?.owner === owner && loaded.query === query ? loaded.data : null;
  useEffect(() => { setUnit(null); setConversion(null); setNotice(""); writer.current?.abort(); setBusy(false); }, [owner, query]);
  useEffect(() => {
    setLoaded(null);
    if (!owner) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/unit-management?${query}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        if (response.status === 401) actions.invalidateSession(owner);
        if (!response.ok) { const body = await response.json().catch(() => null); throw new Error(body?.detail ?? `Catalogue unavailable (${response.status}).`); }
        const body = await response.json() as Snapshot;
        if (!Array.isArray(body.units?.items) || !Array.isArray(body.choices) || !Array.isArray(body.conversions)
          || body.units.page !== Number(params.get("page") ?? 1)
          || (body.item?.itemId ?? null) !== (params.get("itemId") ? Number(params.get("itemId")) : null)) throw new Error("Invalid unit catalogue response.");
        if (!abort.signal.aborted && current.current === owner) setLoaded({ owner, query, data: body });
      } catch (e) { if (!abort.signal.aborted) { setUnit(null); setConversion(null); setNotice(e instanceof Error ? e.message : "Catalogue unavailable."); } }
    })();
    const recheck = () => { setLoaded(null); setRevision(v => v + 1); };
    const timer = window.setInterval(recheck, 30000); window.addEventListener("focus", recheck);
    return () => { abort.abort(); window.clearInterval(timer); window.removeEventListener("focus", recheck); };
  }, [owner, query, params, revision, actions]);
  useEffect(() => () => writer.current?.abort(), []);
  const change = (name: string, value: string) => { const next = new URLSearchParams(params); value ? next.set(name, value) : next.delete(name); if (name === "itemId") next.delete("conversionPage"); setParams(next); };
  async function save(url: string, method: "POST" | "PUT", body: unknown) {
    if (!owner || !data || busy) return;
    const abort = new AbortController(); writer.current = abort; setBusy(true); setNotice("");
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      const token = await bootstrap.json();
      if (!bootstrap.ok || token.headerName !== "X-CSRF-TOKEN" || typeof token.csrfToken !== "string" || !token.csrfToken) throw new Error("CSRF bootstrap unavailable.");
      if (current.current !== owner || abort.signal.aborted) return;
      const response = await fetch(url, { method, credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token.csrfToken }, body: JSON.stringify(body) });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) { const error = await response.json().catch(() => null); throw new Error(error?.detail ?? `Save failed (${response.status}).`); }
      if (!abort.signal.aborted && current.current === owner) { setNotice("MSG08: Saved. Existing documents retain their stored conversion factors."); setUnit(null); setConversion(null); setRevision(v => v + 1); }
    } catch (e) { if (!abort.signal.aborted && current.current === owner) setNotice(e instanceof Error ? e.message : "Save outcome unknown. Reload before retrying."); }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  const name = (id: number) => data?.choices.find(u => u.uomId === id)?.uomName ?? `Unit ${id}`;
  return <section><h1>Units of measurement and item conversions</h1><Link to="/inventory/items">Item catalogue</Link> · <Link to="/inventory/stock">Stock</Link>
    <p>This shared catalogue requires current Storekeeper authority and a global ITEM_MASTER_MANAGE grant. Branch-only grants do not authorize shared catalogue changes.</p>
    {notice && <p role="status">{notice}</p>}
    {data && <>
      <button disabled={busy} onClick={() => { setUnit({ id: null, code: "", name: "", description: "", active: true, version: null }); setConversion(null); }}>Add unit</button>
      <button disabled={busy} onClick={() => { setUnit(null); setConversion(null); setRevision(v => v + 1); }}>Reload/discard edits</button>
      <table><thead><tr><th>Code</th><th>Name</th><th>Status</th><th>Action</th></tr></thead><tbody>{data.units.items.map(u => <tr key={u.uomId}>
        <td>{u.uomCode}</td><td>{u.uomName}</td><td>{u.isActive ? "Active" : "Inactive"}</td><td><button disabled={busy} onClick={() => {
          setUnit({ id: u.uomId, code: u.uomCode, name: u.uomName, description: u.description ?? "", active: u.isActive, version: u.rowVersion }); setConversion(null);
        }}>Edit</button></td></tr>)}</tbody></table>
      {data.units.items.length === 0 && <p>MSG10: No units.</p>}
      <button disabled={busy || !!unit || !!conversion || data.units.page <= 1} onClick={() => change("page", String(data.units.page - 1))}>Previous</button>
      <button disabled={busy || !!unit || !!conversion || data.units.page * data.units.pageSize >= data.units.totalCount} onClick={() => change("page", String(data.units.page + 1))}>Next</button>
      {unit && <form onSubmit={e => { e.preventDefault(); void save(`/api/inventory/units${unit.id ? `/${unit.id}` : ""}`, unit.id ? "PUT" : "POST",
        { uomCode: unit.code, uomName: unit.name, description: unit.description || null, isActive: unit.active, expectedRowVersion: unit.version }); }}>
        <label>Code <input required maxLength={30} value={unit.code} onChange={e => setUnit({ ...unit, code: e.target.value })} /></label>
        <label>Name <input required maxLength={100} value={unit.name} onChange={e => setUnit({ ...unit, name: e.target.value })} /></label>
        <label>Description <input value={unit.description} onChange={e => setUnit({ ...unit, description: e.target.value })} /></label>
        <label><input type="checkbox" checked={unit.active} onChange={e => setUnit({ ...unit, active: e.target.checked })} />Active</label><button disabled={busy}>Save unit</button>
      </form>}
      <h2>Conversions per item</h2>
      <label>Item ID <input type="number" min="1" disabled={busy || !!unit || !!conversion} value={params.get("itemId") ?? ""} onChange={e => change("itemId", e.target.value)} /></label>
      {data.item && <>
        <p>{data.item.itemName}; base unit: {name(data.item.baseUomId)}.</p>
        <button disabled={busy || !data.item.isActive} onClick={() => { setConversion({ id: null, from: "", factor: "", active: true, version: null }); setUnit(null); }}>Add conversion</button>
        <table><thead><tr><th>Additional unit</th><th>Base unit</th><th>Factor</th><th>Status</th><th>Action</th></tr></thead>
          <tbody>{data.conversions.map(c => <tr key={c.conversionId}><td>{name(c.fromUomId)}</td><td>{name(c.toUomId)}</td><td>{c.factor}</td><td>{c.isActive ? "Active" : "Inactive"}</td><td>
            <button disabled={busy} onClick={() => { setConversion({ id: c.conversionId, from: String(c.fromUomId), factor: c.factor, active: c.isActive, version: c.rowVersion }); setUnit(null); }}>Edit/reactivate</button>
            {c.isActive && <button disabled={busy} onClick={() => void save(`/api/inventory/items/${c.itemId}/conversions/${c.conversionId}`, "PUT", { fromUomId: c.fromUomId, conversionFactor: c.factor, isActive: false, expectedRowVersion: c.rowVersion })}>Deactivate</button>}
          </td></tr>)}</tbody></table>
        {data.conversions.length === 0 && <p>MSG10: No additional item conversions.</p>}
        <button disabled={busy || !!unit || !!conversion || data.conversionPage <= 1} onClick={() => change("conversionPage", String(data.conversionPage - 1))}>Previous conversions</button>
        <button disabled={busy || !!unit || !!conversion || data.conversionPage * data.conversionPageSize >= data.conversionTotalCount} onClick={() => change("conversionPage", String(data.conversionPage + 1))}>Next conversions</button>
        {conversion && <form onSubmit={e => { e.preventDefault(); if (!/^\d{1,12}(\.\d{1,6})?$/.test(conversion.factor) || Number(conversion.factor) <= 0) { setNotice("MSG03: Positive factor with up to six decimal places required."); return; }
          void save(`/api/inventory/items/${data.item!.itemId}/conversions${conversion.id ? `/${conversion.id}` : ""}`, conversion.id ? "PUT" : "POST",
            { fromUomId: Number(conversion.from), conversionFactor: conversion.factor, isActive: conversion.active, expectedRowVersion: conversion.version }); }}>
          <label>Additional unit <select required value={conversion.from} onChange={e => setConversion({ ...conversion, from: e.target.value })}><option value="">Select unit</option>
            {data.choices.filter(u => u.uomId !== data.item!.baseUomId && (u.isActive || String(u.uomId) === conversion.from)).map(u => <option key={u.uomId} value={u.uomId}>{u.uomName}</option>)}</select></label>
          <label>Factor to base unit <input required type="number" step="0.000001" min="0.000001" value={conversion.factor} onChange={e => setConversion({ ...conversion, factor: e.target.value })} /></label>
          <label><input type="checkbox" checked={conversion.active} onChange={e => setConversion({ ...conversion, active: e.target.checked })} />Active</label><button disabled={busy}>Save conversion</button>
        </form>}
      </>}
    </>}
  </section>;
}
