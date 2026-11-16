import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import type { Values } from "./contracts";
import { replenishmentCanClose } from "./ReplenishmentCloseFields";

const http = createHttpClient();
type Line = { lineId: number; itemName: string; unitName: string; expectedQty: number; replenishmentLineId: number | null };
type Draft = { documentId: number; documentNo: string; rowVersion: string; lines: Line[] };
type Page = { replenishmentId: number; rowVersion: string; items: Draft[]; nextAfter: number | null };
const id = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const qty = (v: unknown) => v !== "" && v != null && Number.isFinite(Number(v)) && Number(v) >= 0;
function decode(v: unknown): Page | undefined {
  if (!isObject(v) || !id(v.replenishmentId) || typeof v.rowVersion !== "string" || !Array.isArray(v.items)
    || !(v.nextAfter === null || id(v.nextAfter)) || v.items.some(d => !isObject(d) || !id(d.documentId)
      || typeof d.documentNo !== "string" || typeof d.rowVersion !== "string" || !Array.isArray(d.lines) || !d.lines.length
      || d.lines.some(l => !isObject(l) || !id(l.lineId) || typeof l.itemName !== "string" || typeof l.unitName !== "string"
        || typeof l.expectedQty !== "number" || !qty(l.expectedQty) || !(l.replenishmentLineId === null || id(l.replenishmentLineId))))) return undefined;
  return v as Page;
}
export function replenishmentPostReady(record: Values, values: Values): boolean {
  return replenishmentCanClose(record) && values.postSelectionVersion === record.rowVersion
    && values.confirmPostReceipt === true && id(values.receiptDocumentId) && typeof values.receiptExpectedRowVersion === "string"
    && Array.isArray(values.lines) && values.lines.length > 0 && Array.isArray(values.receiptLines) && values.receiptLines.length > 0
    && values.receiptLines.every(l => isObject(l) && id(l.lineId) && qty(l.acceptedQty) && qty(l.rejectedQty) && qty(l.damagedQty)
      && (!(Number(l.acceptedQty) !== Number(l.expectedQty) || Number(l.rejectedQty) > 0 || Number(l.damagedQty) > 0)
        || String(l.discrepancyReason ?? "").trim().length >= 3));
}
export function ReplenishmentPostFields({ record, values, onChange }: { record: Values; values: Values; onChange: (v: Values) => void }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const root = String(record.replenishmentId ?? ""), version = String(record.rowVersion ?? "");
  const [after, setAfter] = useState(0), [revision, setRevision] = useState(0);
  const key = `${root}:${version}:${after}:${revision}`;
  const [loaded, setLoaded] = useState<{ owner: typeof owner; key: string; page?: Page }>();
  useEffect(() => {
    setLoaded(undefined);
    if (owner === null || !id(Number(root)) || !version) return;
    const abort = new AbortController();
    void http({ method: "GET", url: `/api/replenishments/${root}/draft-receipts?after=${after}`, expectedStatus: 200, decode, signal: abort.signal })
      .then(result => {
        if (abort.signal.aborted) return;
        const matches = result.ok && String(result.data.replenishmentId) === root && result.data.rowVersion === version;
        setLoaded({ owner, key, page: matches ? result.data : undefined });
        if (!result.ok && (result.error.kind === "stale" || result.error.kind === "http" && result.error.status === 401)) void actions.revalidate();
      });
    return () => abort.abort();
  }, [owner, root, version, key, after, actions]);
  const current = loaded?.owner === owner && loaded.key === key ? loaded : undefined;
  const selected = current?.page?.items.find(d => d.documentId === values.receiptDocumentId && d.rowVersion === values.receiptExpectedRowVersion);
  const entered = Array.isArray(values.receiptLines) ? values.receiptLines.filter(isObject) : [];
  function navigate(cursor: number) {
    onChange({ ...values, receiptDocumentId: null, receiptExpectedRowVersion: "", receiptLines: [], lines: [], postSelectionVersion: "", confirmPostReceipt: false });
    setAfter(cursor); setRevision(r => r + 1);
  }
  function select(d: Draft) {
    onChange({ ...values, receiptDocumentId: d.documentId, receiptExpectedRowVersion: d.rowVersion, postSelectionVersion: version, confirmPostReceipt: false,
      receiptLines: d.lines.map(l => ({ lineId: l.lineId, expectedQty: l.expectedQty, acceptedQty: "", rejectedQty: 0, damagedQty: 0, discrepancyReason: "" })),
      lines: d.lines.filter(l => l.replenishmentLineId !== null).map(l => ({ stockDocumentLineId: l.lineId, replenishmentLineId: l.replenishmentLineId })) });
  }
  function change(lineId: number, field: string, value: string) {
    onChange({ ...values, confirmPostReceipt: false, receiptLines: entered.map(l => l.lineId === lineId ? { ...l, [field]: value } : l) });
  }
  return <section aria-label="Verify and post receipt">
    <p>Select a Draft receipt in this replenishment's receiving warehouse. Verify every line against the goods received.</p>
    <button type="button" onClick={() => navigate(0)}>Reload Draft receipts (clears entries)</button>
    {!current?.page && <p role="status">{current ? "Draft receipts unavailable or replenishment changed. Reload the record and check current permissions." : "Loading authorized Draft receipts..."}</p>}
    {current?.page?.items.length === 0 && <p>No matching Draft receipts.</p>}
    {current?.page?.items.map(d => <button type="button" key={d.documentId} onClick={() => select(d)}>{d.documentNo}</button>)}
    {current?.page?.nextAfter != null && <button type="button" onClick={() => navigate(current.page!.nextAfter!)}>Next page (clears entries)</button>}
    {selected && <>
      <h3>{selected.documentNo}</h3>
      <p>Posting increases stock for all accepted receipt lines. Only matching items credit this replenishment. Enter quantities in each line's displayed unit.</p>
      {selected.lines.map(l => {
        const input = entered.find(e => e.lineId === l.lineId) ?? {};
        const reasonRequired = Number(input.acceptedQty) !== l.expectedQty || Number(input.rejectedQty) > 0 || Number(input.damagedQty) > 0;
        return <fieldset key={l.lineId}><legend>{l.itemName} ({l.unitName}), expected {l.expectedQty}{l.replenishmentLineId === null ? " — stock only" : " — credits replenishment"}</legend>
          {[['acceptedQty', 'Accepted usable'], ['rejectedQty', 'Rejected'], ['damagedQty', 'Damaged']].map(([field, label]) =>
            <label key={field}>{label}<input required type="number" min="0" step="0.001" value={String(input[field] ?? "")} onChange={e => change(l.lineId, field, e.target.value)} /></label>)}
          <label>Discrepancy reason<textarea required={reasonRequired} minLength={reasonRequired ? 3 : undefined}
            value={String(input.discrepancyReason ?? "")} onChange={e => change(l.lineId, "discrepancyReason", e.target.value)} /></label>
        </fieldset>;
      })}
      <label><input type="checkbox" required checked={values.confirmPostReceipt === true} onChange={e => onChange({ ...values, confirmPostReceipt: e.target.checked })} />
        I verified every line. Post this receipt and credit the matching replenishment items.</label>
    </>}
  </section>;
}
