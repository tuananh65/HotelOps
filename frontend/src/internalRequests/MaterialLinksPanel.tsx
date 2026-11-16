import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface Item {
  materialRequestId: number; code: string; purpose: string; state: string; mandatory: boolean;
  completionSatisfied: boolean | null; completionUnavailableReason: string | null;
}
interface View { requestId: number; branchId: number; version: string; items: Item[]; total: number; page: number }
class Failure extends Error { constructor(readonly status: number) { super("Linked materials unavailable"); } }
const positive = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;

export function InternalRequestMaterialLinksPanel({ id }: { id: number }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{ owner: typeof owner; key: string; view?: View; error?: string } | null>(null);
  const key = `${id}:${page}:${revision}`;
  useEffect(() => {
    const abort = new AbortController();
    setLoaded(null);
    if (owner) void fetch(`/api/internal-requests/${id}/linked-materials?page=${page}`, {
      credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
    }).then(async response => {
      if (!response.ok) throw new Failure(response.status);
      if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("Invalid response");
      const view = await response.json() as View;
      if (!view || view.requestId !== id || !positive(view.branchId) || view.page !== page
        || typeof view.version !== "string" || !/^[A-Za-z0-9+/]{11}=$/.test(view.version)
        || !Number.isSafeInteger(view.total) || view.total < 0 || !Array.isArray(view.items) || view.items.length > 25
        || view.items.length > Math.max(0, view.total - (page - 1) * 25)
        || view.items.some(item => !item || !positive(item.materialRequestId) || typeof item.code !== "string"
          || typeof item.purpose !== "string" || typeof item.state !== "string" || typeof item.mandatory !== "boolean"
          || !(item.completionSatisfied === null || typeof item.completionSatisfied === "boolean")
          || !(item.completionUnavailableReason === null || typeof item.completionUnavailableReason === "string")
          || item.mandatory !== (item.completionSatisfied !== null)
          || item.completionSatisfied === true && item.completionUnavailableReason !== null)
        || new Set(view.items.map(item => item.materialRequestId)).size !== view.items.length)
        throw new Error("Invalid linked material response");
      if (!abort.signal.aborted) setLoaded({ owner, key, view });
    }).catch(error => {
      if (abort.signal.aborted) return;
      setLoaded({ owner, key, error: "Linked materials unavailable. Reload to check current access." });
      if (error instanceof Failure && error.status === 401) actions.invalidateSession(owner!);
    });
    return () => abort.abort();
  }, [id, owner, page, revision, key, actions]);
  if (!owner) return null;
  const visible = loaded?.owner === owner && loaded.key === key ? loaded : null;
  return <section aria-label="Linked material requests" aria-busy={!visible}>
    <h3>Linked material requests</h3>
    <p>Only materials covered by your current MR_VIEW permission are listed. An empty list does not confirm that the request can be closed.</p>
    <button onClick={() => setRevision(value => value + 1)}>Reload linked materials</button>
    {visible?.error && <p role="status">{visible.error}</p>}
    {visible?.view && <>
      {visible.view.items.length === 0 && <p>No visible linked materials on this page.</p>}
      <ul>{visible.view.items.map(item => <li key={item.materialRequestId}>
        <strong>{item.code}</strong>: {item.purpose} — {item.state}
        <p>{!item.mandatory ? "Optional for completion" : item.completionSatisfied
          ? "Required material: completion evidence confirmed"
          : "Required material: completion is not confirmed. Remaining work or missing evidence blocks closing."}</p>
      </li>)}</ul>
      <button disabled={page <= 1} onClick={() => setPage(value => value - 1)}>Previous materials</button>
      <span> Page {page} </span>
      <button disabled={page * 25 >= visible.view.total} onClick={() => setPage(value => value + 1)}>Next materials</button>
    </>}
  </section>;
}
