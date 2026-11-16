import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { Link } from "react-router-dom";

interface Item {
  materialRequestId: number; code: string; purpose: string; state: string; mandatory: boolean;
  completionSatisfied: boolean | null; completionUnavailableReason: string | null;
}
interface View { requestId: number; branchId: number; version: string; items: Item[]; total: number; page: number; candidates: boolean; canLink: boolean }
class Failure extends Error { constructor(readonly status: number) { super("Linked materials unavailable"); } }
const positive = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const version = (v: unknown): v is string => typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
async function json(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  const response = await fetch(path, { ...init, credentials: "same-origin", cache: "no-store", redirect: "error", signal });
  if (!response.ok) throw new Failure(response.status);
  if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("Invalid response");
  return response.json();
}

export function InternalRequestMaterialLinksPanel({ id, onLinked }: { id: number; onLinked?: () => void }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [candidates, setCandidates] = useState(false);
  const [selected, setSelected] = useState("");
  const [mandatory, setMandatory] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  const [loaded, setLoaded] = useState<{ owner: typeof owner; key: string; view?: View; error?: string } | null>(null);
  const key = `${id}:${candidates}:${page}:${revision}`;
  useEffect(() => {
    const abort = new AbortController(); controller.current = abort;
    setLoaded(null); setSelected(""); setMandatory(""); setBusy(false); setMessage(""); sending.current = false;
    if (owner) void json(`/api/internal-requests/${id}/linked-materials?candidates=${candidates}&page=${page}`, abort.signal).then(raw => {
      const view = raw as View;
      if (!view || view.requestId !== id || !positive(view.branchId) || view.page !== page
        || !version(view.version) || view.candidates !== candidates || typeof view.canLink !== "boolean"
        || candidates && !view.canLink
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
  }, [id, owner, candidates, page, revision, key, actions]);
  if (!owner) return null;
  const visible = loaded?.owner === owner && loaded.key === key ? loaded : null;
  async function link() {
    const abort = controller.current;
    const view = visible?.view;
    if (!owner || !view?.canLink || !candidates || sending.current || !abort || abort.signal.aborted) return;
    const materialRequestId = Number(selected);
    if (!view.items.some(item => item.materialRequestId === materialRequestId) || !["required", "optional"].includes(mandatory)) {
      setMessage("Select a material request and its completion requirement."); return;
    }
    sending.current = true; setBusy(true); setMessage("");
    try {
      const csrf = await json("/api/auth/csrf", abort.signal) as { csrfToken?: unknown };
      if (typeof csrf?.csrfToken !== "string" || !csrf.csrfToken || abort.signal.aborted) throw new Error("Security check failed");
      const raw = await json(`/api/internal-requests/${id}/linked-materials`, abort.signal, {
        method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": `"${view.version}"` },
        body: JSON.stringify({ materialRequestId, mandatory: mandatory === "required" }),
      }) as { requestId?: unknown; materialRequestId?: unknown; mandatory?: unknown; version?: unknown; message?: unknown };
      if (!raw || raw.requestId !== id || raw.materialRequestId !== materialRequestId || raw.mandatory !== (mandatory === "required")
        || !version(raw.version) || raw.message !== "MSG08") throw new Error("Link not confirmed");
      if (!abort.signal.aborted) { setLoaded(null); setMessage("MSG08: Material request linked. Reload linked materials."); onLinked?.(); }
    } catch (error) {
      if (!abort.signal.aborted) {
        setLoaded(null);
        setMessage(error instanceof Failure && error.status === 412 ? "Request changed. Reload before linking."
          : "Link not confirmed. Reload current access and state before retrying.");
        if (error instanceof Failure && error.status === 401) actions.invalidateSession(owner);
      }
    } finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); } }
  }
  return <section aria-label="Linked material requests" aria-busy={busy || !visible}>
    <h3>Linked material requests</h3>
    <p>Only materials covered by your current MR_VIEW permission are listed. An empty list does not confirm that the request can be closed.</p>
    <button disabled={busy} onClick={() => setRevision(value => value + 1)}>Reload linked materials</button>
    {message && <p role="status">{message}</p>}
    {candidates && <button disabled={busy} onClick={() => { setCandidates(false); setPage(1); }}>View linked materials</button>}
    {!candidates && visible?.view?.canLink && <button disabled={busy} onClick={() => { setCandidates(true); setPage(1); }}>Link existing material request</button>}
    {visible?.view?.canLink && <Link to={`/operations/material-requests?action=A004A-043&branchId=${visible.view.branchId}&internalRequestId=${id}&sourceVersion=${encodeURIComponent(visible.view.version)}`}>Create linked material request</Link>}
    {visible?.error && <p role="status">{visible.error}</p>}
    {visible?.view && <>
      {visible.view.items.length === 0 && <p>No visible {candidates ? "open" : "linked"} materials on this page.</p>}
      <ul>{visible.view.items.map(item => <li key={item.materialRequestId}>
        <Link to={`/operations/material-requests/${item.materialRequestId}`}>Open material request</Link>{" "}
        <strong>{item.code}</strong>: {item.purpose} — {item.state}
        {!candidates && <p>{!item.mandatory ? "Optional for completion" : item.completionSatisfied
          ? "Required material: completion evidence confirmed"
          : "Required material: completion is not confirmed. Remaining work or missing evidence blocks closing."}</p>}
      </li>)}</ul>
      <button disabled={busy || page <= 1} onClick={() => setPage(value => value - 1)}>Previous materials</button>
      <span> Page {page} </span>
      <button disabled={busy || page * 25 >= visible.view.total} onClick={() => setPage(value => value + 1)}>Next materials</button>
      {candidates && visible.view.canLink && <>
        <label>Open material request<select value={selected} disabled={busy} onChange={event => setSelected(event.target.value)}>
          <option value="">Select material request</option>{visible.view.items.map(item => <option key={item.materialRequestId} value={item.materialRequestId}>{item.code}: {item.purpose}</option>)}
        </select></label>
        <label>Completion requirement<select value={mandatory} disabled={busy} onChange={event => setMandatory(event.target.value)}>
          <option value="">Select requirement</option><option value="required">Required for completion</option><option value="optional">Optional</option>
        </select></label>
        <button disabled={busy} onClick={() => void link()}>Save material link</button>
      </>}
    </>}
  </section>;
}
