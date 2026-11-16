import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import type { Values } from "./contracts";

type Choice = { id: number; name: string };
type Item = Choice & { code: string; units: Choice[] };
type Catalog = { branchId: number; departments: Choice[]; warehouses: Choice[]; rooms: Choice[];
  areas: Choice[]; priorities: Choice[]; incidents: Choice[]; items: Item[] };
const request = createHttpClient();
const positive = (n: unknown): n is number => typeof n === "number" && Number.isSafeInteger(n) && n > 0;
function choices(value: unknown): value is Choice[] {
  return Array.isArray(value) && value.every(v => isObject(v) && positive(v.id) && typeof v.name === "string")
    && new Set(value.map(v => v.id)).size === value.length;
}
function decode(raw: unknown): Catalog | undefined {
  if (!isObject(raw) || !positive(raw.branchId) || !choices(raw.departments) || !choices(raw.warehouses)
    || !choices(raw.rooms) || !choices(raw.areas) || !choices(raw.priorities) || !choices(raw.incidents)
    || !choices(raw.items) || raw.items.some((i: unknown) => !isObject(i) || typeof i.code !== "string" || !choices(i.units) || i.units.length === 0)) return undefined;
  return raw as Catalog;
}

export function MaterialCatalogFields({ values, onChange, historicalLineMax }: {
  values: Values; onChange: (value: Values) => void; historicalLineMax?: number;
}) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const branch = String(values.branchId ?? "");
  const [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{ owner: typeof owner; branch: string; catalog?: Catalog; error?: string }>();
  useEffect(() => {
    setLoaded(undefined);
    if (!owner || !/^[1-9]\d*$/.test(branch) || !Number.isSafeInteger(Number(branch))) return;
    const abort = new AbortController();
    void request({ method: "GET", url: `/api/material-requests/creation-catalog?branchId=${branch}`,
      expectedStatus: 200, decode, signal: abort.signal }).then(result => {
      if (abort.signal.aborted) return;
      setLoaded(result.ok && result.data.branchId === Number(branch) ? { owner, branch, catalog: result.data }
        : { owner, branch, error: "Material choices unavailable. Check current access and reload." });
      if (!result.ok && result.error.kind === "http" && result.error.status === 401) void actions.revalidate();
    });
    return () => abort.abort();
  }, [owner, branch, revision, actions]);
  const current = loaded?.owner === owner && loaded.branch === branch ? loaded : undefined;
  const catalog = current?.catalog;
  const lines = Array.isArray(values.lines) ? values.lines as Values[] : [];
  function selector(name: string, title: string, items: Choice[], required = false) {
    return <label>{title}<select name={name} required={required}
      value={items.some(i => String(i.id) === String(values[name])) ? String(values[name]) : ""}
      onChange={e => onChange({ ...values, [name]: e.target.value,
        ...(name === "sourceWarehouseId" ? { assignedApproverUserId: "" } : {}),
        ...(name === "receivingRoomId" && e.target.value ? { receivingAreaId: "" } : {}),
        ...(name === "receivingAreaId" && e.target.value ? { receivingRoomId: "" } : {}) })}>
      <option value="">{required ? "Select an option" : "None"}</option>
      {items.map(i => <option key={i.id} value={i.id}>{i.name}</option>)}
    </select></label>;
  }
  function updateLine(index: number, changes: Values) {
    onChange({ ...values, lines: lines.map((line, n) => n === index ? { ...line, ...changes } : line) });
  }
  return <section aria-label="Material choices">
    <button type="button" onClick={() => setRevision(v => v + 1)}>Reload material choices</button>
    {!catalog && <p role="status">{current?.error ?? (branch ? "Loading material choices…" : "Select a branch first.")}</p>}
    {selector("departmentId", "Receiving department", catalog?.departments ?? [], true)}
    {selector("sourceWarehouseId", "Issuing warehouse", catalog?.warehouses ?? [], values.submit === true)}
    {selector("receivingRoomId", "Receiving room (optional)", catalog?.rooms ?? [])}
    {selector("receivingAreaId", "Receiving area (optional)", catalog?.areas ?? [])}
    {selector("priorityId", "Priority (optional)", catalog?.priorities ?? [])}
    {selector("incidentId", "Linked incident (optional)", catalog?.incidents ?? [])}
    <fieldset><legend>Requested materials</legend>
      {lines.map((line, index) => {
        const item = catalog?.items.find(i => String(i.id) === String(line.itemId));
        const persisted = historicalLineMax !== undefined && line.requestLineId != null;
        return <fieldset key={String(line.lineNo)}><legend>Line {String(line.lineNo)}</legend>
          {persisted && <p>To change material or unit, remove this line and add a new one. Its history is retained.</p>}
          <label>Item<select required disabled={persisted} value={item ? String(item.id) : ""}
            onChange={e => updateLine(index, { itemId: e.target.value, uomId: "" })}>
            <option value="">Select material</option>{catalog?.items.map(i => <option key={i.id} value={i.id}>{i.code}: {i.name}</option>)}
          </select></label>
          <label>Unit<select required disabled={persisted} value={item?.units.some(u => String(u.id) === String(line.uomId)) ? String(line.uomId) : ""}
            onChange={e => updateLine(index, { uomId: e.target.value })}>
            <option value="">Select unit</option>{item?.units.map(u => <option key={u.id} value={u.id}>{u.name}</option>)}
          </select></label>
          <label>Quantity<input type="number" min="0.001" step="0.001" required value={String(line.requestedQty ?? "")}
            onChange={e => updateLine(index, { requestedQty: e.target.value })} /></label>
          <label>Note (optional)<input value={String(line.note ?? "")} onChange={e => updateLine(index, { note: e.target.value })} /></label>
          <button type="button" onClick={() => onChange({ ...values, lines: lines.filter((_, n) => n !== index) })}>Remove line</button>
        </fieldset>;
      })}
      {lines.length === 0 && <p>Add at least one material line to save the request.</p>}
      <button type="button" disabled={!catalog?.items.length}
        onClick={() => onChange({ ...values, lines: [...lines, { lineNo: Math.max(historicalLineMax ?? 0, 0, ...lines.map(l => Number(l.lineNo))) + 1, itemId: "", uomId: "", requestedQty: "", note: "" }] })}>Add material</button>
    </fieldset>
  </section>;
}
