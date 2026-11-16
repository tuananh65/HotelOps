import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import type { Values } from "./contracts";

const request = createHttpClient();
type Candidate = { stockDocumentLineId: number; replenishmentLineId: number; documentNo: string; itemName: string; baseQty: number; unitName: string; postedAt: string };
type Page = { replenishmentId: number; rowVersion: string; items: Candidate[]; nextAfter: number | null };
const positive = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
function decode(v: unknown): Page | undefined {
  if (!isObject(v) || !positive(v.replenishmentId) || typeof v.rowVersion !== "string"
    || !(v.nextAfter === null || positive(v.nextAfter)) || !Array.isArray(v.items)
    || v.items.some(c => !isObject(c) || !positive(c.stockDocumentLineId) || !positive(c.replenishmentLineId)
      || typeof c.documentNo !== "string" || typeof c.itemName !== "string" || typeof c.unitName !== "string"
      || typeof c.baseQty !== "number" || !Number.isFinite(c.baseQty) || c.baseQty <= 0 || typeof c.postedAt !== "string")) return undefined;
  return v as Page;
}
export function ReplenishmentReceiptFields({ record, values, onChange }: { record: Values; values: Values; onChange: (v: Values) => void }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const id = String(record.replenishmentId ?? ""), version = String(record.rowVersion ?? "");
  const [after, setAfter] = useState(0), [revision, setRevision] = useState(0);
  const key = `${id}:${version}:${after}:${revision}`;
  const [loaded, setLoaded] = useState<{ owner: typeof owner; key: string; page?: Page }>();
  useEffect(() => {
    setLoaded(undefined);
    if (!owner || !positive(Number(id)) || !version) return;
    const abort = new AbortController();
    void request({ method: "GET", url: `/api/replenishments/${id}/receipt-candidates?after=${after}`,
      expectedStatus: 200, decode, signal: abort.signal }).then(result => {
      if (abort.signal.aborted) return;
      const matches = result.ok && String(result.data.replenishmentId) === id && result.data.rowVersion === version;
      setLoaded({ owner, key, page: matches ? result.data : undefined });
      if (!result.ok && (result.error.kind === "stale" || result.error.kind === "http" && result.error.status === 401)) void actions.revalidate();
    });
    return () => abort.abort();
  }, [owner, id, version, after, key, actions]);
  const current = loaded?.owner === owner && loaded.key === key ? loaded : undefined;
  const page = current?.page;
  const selected = Array.isArray(values.lines) ? values.lines.filter(isObject) : [];
  function navigate(cursor: number) { onChange({ ...values, lines: [], receiptSelectionVersion: "" }); setAfter(cursor); setRevision(v => v + 1); }
  return <section aria-label="Link posted receipts">
    <p>Select whole posted receipt lines. This records received quantities; it does not post stock again.</p>
    <button type="button" onClick={() => navigate(0)}>Refresh receipt candidates</button>
    {!page && <p role="status">{current ? "Candidates unavailable or request changed. Reload the replenishment before linking." : "Loading authorized receipt candidates…"}</p>}
    {page?.items.length === 0 && <p>No uncredited matching receipt lines fit the remaining quantity.</p>}
    {page?.items.map(c => <label key={`${c.stockDocumentLineId}:${c.replenishmentLineId}`}>
      <input type="checkbox" checked={selected.some(l => l.stockDocumentLineId === c.stockDocumentLineId)}
        onChange={e => onChange({ ...values, receiptSelectionVersion: version, lines: e.target.checked
          ? [...selected.filter(l => l.stockDocumentLineId !== c.stockDocumentLineId), { stockDocumentLineId: c.stockDocumentLineId, replenishmentLineId: c.replenishmentLineId }]
          : selected.filter(l => l.stockDocumentLineId !== c.stockDocumentLineId) })} />
      {c.documentNo} — {c.itemName}: {c.baseQty} {c.unitName}, posted {c.postedAt}
    </label>)}
    {page?.nextAfter != null && <button type="button" onClick={() => navigate(page.nextAfter!)}>Next receipt page (clears selection)</button>}
    <p>Combined quantities must fit the remaining amount. The server rechecks receipt eligibility and prevents duplicate credit when saving.</p>
  </section>;
}
