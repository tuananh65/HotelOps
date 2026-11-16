import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { InternalRequestTaskCreationPanel } from "./TaskCreationPanel";

interface Item { taskId: number; code: string; title: string; state: string | null; mandatory: boolean | null; path: string }
interface View { requestId: number; branchId: number; version: string; canLink: boolean; items: Item[]; total: number; page: number; candidates: boolean }
const validVersion = (v: unknown): v is string => typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
class Failure extends Error { constructor(readonly status: number) { super("Linked work unavailable"); } }
async function json(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  const response = await fetch(path, { ...init, credentials: "same-origin", cache: "no-store", redirect: "error", signal });
  if (!response.ok) throw new Failure(response.status);
  if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("Invalid response");
  return response.json();
}
export function InternalRequestTaskLinksPanel({ id, onLinked }: { id: number; onLinked?: () => void }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.pending === null && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [candidates, setCandidates] = useState(false); const [page, setPage] = useState(1); const [revision, setRevision] = useState(0);
  const [creating, setCreating] = useState(false);
  const [loaded, setLoaded] = useState<{ owner: typeof owner; key: string; view: View } | null>(null);
  const [selected, setSelected] = useState(""); const [mandatory, setMandatory] = useState("");
  const [busy, setBusy] = useState(false); const [message, setMessage] = useState("");
  const sending = useRef(false); const controller = useRef<AbortController | null>(null);
  const key = `${id}:${candidates}:${page}:${revision}`;
  useEffect(() => {
    const abort = new AbortController(); controller.current = abort;
    setLoaded(null); setSelected(""); setMandatory(""); setMessage(""); setBusy(false); sending.current = false;
    if (owner) void json(`/api/internal-requests/${id}/linked-tasks?candidates=${candidates}&page=${page}`, abort.signal).then(raw => {
      const v = raw as View;
      if (!v || v.requestId !== id || !Number.isSafeInteger(v.branchId) || v.branchId <= 0 || !validVersion(v.version) || typeof v.canLink !== "boolean" || v.candidates !== candidates || v.page !== page
        || !Number.isSafeInteger(v.total) || v.total < 0 || !Array.isArray(v.items) || v.items.length > 25 || v.items.length > v.total
        || v.items.some(t => !Number.isSafeInteger(t.taskId) || t.taskId <= 0 || typeof t.code !== "string" || typeof t.title !== "string"
          || !(t.state === null || typeof t.state === "string") || !(t.mandatory === null || typeof t.mandatory === "boolean") || typeof t.path !== "string"
          || !new RegExp(`^/operational-tasks\\?branchId=[1-9][0-9]*&taskId=${t.taskId}$`).test(t.path))) throw new Error("Invalid linked work response");
      if (!abort.signal.aborted) setLoaded({ owner, key, view: v });
    }).catch(e => { if (!abort.signal.aborted) { setMessage("Linked tasks unavailable. Reload to check current access."); if (e instanceof Failure && e.status === 401) actions.invalidateSession(owner!); } });
    return () => abort.abort();
  }, [id, owner, candidates, page, revision, key, actions]);
  const view = owner && loaded?.owner === owner && loaded.key === key ? loaded.view : null;
  async function link() {
    const abort = controller.current;
    if (!owner || !view?.canLink || !candidates || !abort || abort.signal.aborted || sending.current) return;
    const taskId = Number(selected);
    if (!view.items.some(t => t.taskId === taskId) || !["required", "optional"].includes(mandatory)) { setMessage("Select a task and whether it is required for completion."); return; }
    sending.current = true; setBusy(true); setMessage("");
    try {
      const csrf = await json("/api/auth/csrf", abort.signal) as { csrfToken?: unknown };
      if (typeof csrf.csrfToken !== "string" || !csrf.csrfToken || abort.signal.aborted) throw new Error("Security check failed");
      const raw = await json(`/api/internal-requests/${id}/linked-tasks`, abort.signal, { method: "POST",
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": `"${view.version}"` },
        body: JSON.stringify({ taskId, mandatory: mandatory === "required" }) }) as { requestId?: unknown; taskId?: unknown; mandatory?: unknown; version?: unknown };
      if (raw.requestId !== id || raw.taskId !== taskId || raw.mandatory !== (mandatory === "required") || !validVersion(raw.version)) throw new Error("Link not confirmed");
      if (!abort.signal.aborted) { setLoaded(null); setMessage("MSG08: Task linked. View linked tasks to see the saved record."); onLinked?.(); }
    } catch (e) { if (!abort.signal.aborted) { setLoaded(null); setMessage(e instanceof Failure && e.status === 412 ? "Request changed. Reload linked tasks before saving." : "Link not confirmed. Reload before retrying."); if (e instanceof Failure && e.status === 401) actions.invalidateSession(owner); } }
    finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); } }
  }
  if (!owner) return null;
  return <section aria-label="Linked operational tasks"><h3>Linked operational tasks</h3>
    <p>Only tasks you can currently view are listed. Linking work does not close the request.</p>
    {message && <p role="status">{message}</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload linked tasks</button>
    {candidates && <button disabled={busy} onClick={() => { setCandidates(false); setPage(1); }}>View linked tasks</button>}
    {!candidates && view?.canLink && <button disabled={busy} onClick={() => { setCandidates(true); setPage(1); }}>Link existing task</button>}
    {!candidates && view?.canLink && <button disabled={busy} onClick={() => setCreating(true)}>Create new operational task</button>}
    {!candidates && view?.canLink && creating && <InternalRequestTaskCreationPanel key={`${owner}:${id}:${revision}`} id={id} branch={view.branchId} onLinked={onLinked} />}
    {view && <>
      {view.items.length === 0 && <p>No visible {candidates ? "open" : "linked"} tasks on this page.</p>}
      <ul>{view.items.map(t => <li key={t.taskId}><Link to={t.path}>{t.code}: {t.title}</Link> — {t.state ?? "Status unavailable"}
        {!candidates && <span> — {t.mandatory === null ? "Completion requirement unclassified" : t.mandatory ? "Required for completion" : "Optional"}</span>}</li>)}</ul>
      <button disabled={busy || page <= 1} onClick={() => setPage(p => p - 1)}>Previous tasks</button><span> Page {page} </span>
      <button disabled={busy || page * 25 >= view.total} onClick={() => setPage(p => p + 1)}>Next tasks</button>
      {candidates && view.canLink && <div aria-busy={busy}>
        <label>Open task<select aria-label="Open task" disabled={busy} value={selected} onChange={e => setSelected(e.target.value)}><option value="">Select task</option>
          {view.items.map(t => <option key={t.taskId} value={t.taskId}>{t.code}: {t.title}</option>)}</select></label>
        <label>Completion requirement<select aria-label="Completion requirement" disabled={busy} value={mandatory} onChange={e => setMandatory(e.target.value)}><option value="">Select requirement</option>
          <option value="required">Required for completion</option><option value="optional">Optional</option></select></label>
        <button disabled={busy} onClick={() => void link()}>Save task link</button>
      </div>}
    </>}
  </section>;
}
