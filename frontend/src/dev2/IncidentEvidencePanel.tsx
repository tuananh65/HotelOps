import { useEffect, useState } from "react";
import { createHttpClient, isObject } from "../auth/http";

type Item = { id: number; name: string; note: string | null; authorId: number; uploadedAt: string };
type Page = { incidentId: number; page: number; total: number; items: Item[] };
const request = createHttpClient();
export function IncidentEvidencePanel({ incidentId }: { incidentId: number }) {
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{ key: string; data?: Page; error?: string }>();
  const key = `${incidentId}:${page}:${revision}`;
  useEffect(() => {
    const abort = new AbortController();
    void request<Page>({ method: "GET", url: `/api/incidents/${incidentId}/evidence?page=${page}`, expectedStatus: 200, signal: abort.signal,
      decode: raw => {
        if (!isObject(raw) || raw.incidentId !== incidentId || raw.page !== page || !Number.isSafeInteger(raw.total)
          || Number(raw.total) < 0 || !Array.isArray(raw.items) || raw.items.length !== Math.min(25, Math.max(0, Number(raw.total) - (page - 1) * 25))) return undefined;
        const ids = new Set<number>();
        for (const item of raw.items) {
          if (!isObject(item) || typeof item.id !== "number" || !Number.isSafeInteger(item.id) || item.id <= 0 || ids.has(item.id)
            || typeof item.name !== "string" || !(item.note === null || typeof item.note === "string")
            || typeof item.authorId !== "number" || !Number.isSafeInteger(item.authorId) || item.authorId <= 0
            || typeof item.uploadedAt !== "string" || !Number.isFinite(Date.parse(item.uploadedAt))) return undefined;
          ids.add(item.id);
        }
        return raw as unknown as Page;
      },
    }).then(result => { if (!abort.signal.aborted) setLoaded({ key, ...(result.ok ? { data: result.data }
      : { error: "Incident evidence is unavailable. Reload to check current access." }) }); });
    return () => abort.abort();
  }, [incidentId, page, key]);
  const current = loaded?.key === key ? loaded : undefined;
  return <section aria-label="Incident evidence"><h3>Incident evidence</h3>
    <button type="button" onClick={() => setRevision(r => r + 1)}>Reload evidence</button>
    {!current && <p role="status">Loading evidence…</p>}
    {current?.error && <p role="alert">{current.error}</p>}
    {current?.data && <>
      {!current.data.total && <p>No attachments recorded.</p>}
      <ul>{current.data.items.map(item => <li key={item.id}>
        <a href={`/api/incidents/${incidentId}/evidence/${item.id}`} download>{item.name}</a>
        <p>Uploaded by user {item.authorId} at {item.uploadedAt}</p>{item.note && <p>{item.note}</p>}
      </li>)}</ul>
      <button type="button" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous evidence</button>
      <span> Page {page} </span><button type="button" disabled={page * 25 >= current.data.total} onClick={() => setPage(p => p + 1)}>Next evidence</button>
    </>}
  </section>;
}
