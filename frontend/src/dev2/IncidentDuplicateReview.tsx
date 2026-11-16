import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { createHttpClient, isObject } from "../auth/http";
import type { Values } from "./contracts";

export function incidentLocationKey(values: Values): string | null {
  const parts = [values.branchId, values.roomId, values.areaId, values.assetId].map(v => String(v ?? ""));
  if (!parts[0] || !parts.slice(1).some(Boolean)
    || parts.some(v => v !== "" && (!/^[1-9]\d*$/.test(v) || !Number.isSafeInteger(Number(v))))) return null;
  return parts.join(":");
}
type Item = { incidentId: number; incidentNo: string; incidentTitle: string; status: string };
type Page = { items: Item[]; totalCount: number; totalPages: number; page: number; similarReviewToken: string };
export type IncidentReview = { key: string; token: string };
const request = createHttpClient();
export function IncidentDuplicateReview({ values, onReviewed }: { values: Values; onReviewed: (review: IncidentReview | null) => void }) {
  const key = incidentLocationKey(values);
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{ key: string; page: number; data?: Page; error?: string }>();
  useEffect(() => { setPage(1); }, [key]);
  useEffect(() => {
    onReviewed(null);
    setLoaded(undefined);
    if (!key) return;
    const abort = new AbortController();
    const [branchId, roomId, areaId, assetId] = key.split(":");
    const query = new URLSearchParams({ branchId, similarOpenOnly: "true", page: String(page), pageSize: "20" });
    for (const [name, value] of Object.entries({ roomId, areaId, assetId })) if (value) query.set(name, value);
    void request<Page>({ method: "GET", url: `/api/incidents?${query}`, expectedStatus: 200, signal: abort.signal,
      decode: raw => {
        if (!isObject(raw) || typeof raw.similarReviewToken !== "string" || !raw.similarReviewToken || raw.similarReviewToken.length > 4096
          || raw.page !== page || raw.pageSize !== 20 || !Number.isSafeInteger(raw.totalCount)
          || Number(raw.totalCount) < 0 || raw.totalPages !== Math.ceil(Number(raw.totalCount) / 20)
          || !Array.isArray(raw.items) || raw.items.length !== Math.min(20, Math.max(0, Number(raw.totalCount) - (page - 1) * 20))) return undefined;
        const ids = new Set<number>();
        for (const item of raw.items) {
          if (!isObject(item) || typeof item.incidentId !== "number" || !Number.isSafeInteger(item.incidentId) || item.incidentId <= 0
            || ids.has(item.incidentId) || item.branchId !== Number(branchId) || typeof item.incidentNo !== "string"
            || typeof item.incidentTitle !== "string" || !["OPEN", "IN_PROGRESS", "RESOLVED"].includes(String(item.status))
            || !(roomId && item.roomId === Number(roomId) || areaId && item.areaId === Number(areaId) || assetId && item.assetId === Number(assetId))) return undefined;
          ids.add(item.incidentId);
        }
        return raw as unknown as Page;
      },
    }).then(result => {
      if (!abort.signal.aborted) setLoaded({ key, page, ...(result.ok ? { data: result.data }
        : { error: "Similar incidents could not be checked. Current incident read access is required. Reload before creating." }) });
    });
    return () => abort.abort();
  }, [key, page, revision, onReviewed]);
  const current = loaded?.key === key && loaded.page === page ? loaded : undefined;
  return <section aria-label="Review similar incidents">
    <h3>Review similar incidents</h3>
    <p>Incidents not yet closed at any selected location are shown within your read access. An empty result does not prove there are no other incidents.</p>
    {!key ? <p>Select a branch and a room, area or asset.</p> : !current ? <p role="status">Checking similar incidents…</p>
      : current.error ? <p role="alert">{current.error}</p> : current.data && <>
        {!current.data.totalCount && <p>No visible matching incidents.</p>}
        <ul>{current.data.items.map(item => <li key={item.incidentId}>
          <Link to={`/operations/incidents/${item.incidentId}`}>{item.incidentNo}: {item.incidentTitle}</Link> — {item.status}
        </li>)}</ul>
        <button type="button" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous similar incidents</button>
        <span> Page {page} </span>
        <button type="button" disabled={page >= current.data.totalPages} onClick={() => setPage(p => p + 1)}>Next similar incidents</button>
        <label><input key={`${key}:${page}:${revision}`} type="checkbox" onChange={event => onReviewed(event.target.checked && key
          ? { key, token: current.data!.similarReviewToken } : null)} />
          I reviewed the visible matches and want to create a new incident.</label>
      </>}
    <button type="button" disabled={!key} onClick={() => setRevision(r => r + 1)}>Reload similar incidents</button>
  </section>;
}
