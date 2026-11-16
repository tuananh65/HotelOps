import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createAuthApi } from "../auth/authApi";
import { createHttpClient, isObject } from "../auth/http";
import type { Values } from "./contracts";

const http = createHttpClient(), auth = createAuthApi();
type Row = { taskId: number; code: string; title: string; state: string | null };
type View = { requestId: number; branchId: number; version: string; canLink: boolean; candidates: boolean; page: number; total: number; items: Row[] };
const id = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
function decode(v: unknown): View | undefined {
  if (!isObject(v) || !id(v.requestId) || !id(v.branchId) || typeof v.version !== "string" || !/^[A-Za-z0-9+/]{11}=$/.test(v.version)
    || typeof v.canLink !== "boolean" || typeof v.candidates !== "boolean" || !id(v.page)
    || typeof v.total !== "number" || !Number.isSafeInteger(v.total) || v.total < 0 || !Array.isArray(v.items)
    || v.items.some(r => !isObject(r) || !id(r.taskId) || typeof r.code !== "string" || typeof r.title !== "string"
      || !(r.state === null || typeof r.state === "string"))) return undefined;
  return v as View;
}

export function MaterialTaskLinks({ record }: { record: Values }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const user = state.session?.user.id;
  const [candidates, setCandidates] = useState(false), [page, setPage] = useState(1), [revision, setRevision] = useState(0);
  const key = `${record.requestId}:${record.rowVersion}:${candidates}:${page}:${revision}`;
  const [loaded, setLoaded] = useState<{ owner: typeof owner; key: string; data?: View; error?: string }>();
  const [busy, setBusy] = useState(false), [message, setMessage] = useState("");
  const sending = useRef(false), lifetime = useRef<AbortController | null>(null);
  useEffect(() => {
    const abort = new AbortController(); lifetime.current = abort; setLoaded(undefined); setBusy(false); sending.current = false;
    if (owner !== null && id(record.requestId)) void http({ method: "GET",
      url: `/api/material-requests/${record.requestId}/linked-tasks?candidates=${candidates}&page=${page}`,
      expectedStatus: 200, decode, signal: abort.signal }).then(result => {
      if (abort.signal.aborted) return;
      const matching = result.ok && result.data.requestId === record.requestId && result.data.branchId === record.branchId
        && result.data.candidates === candidates && result.data.page === page;
      setLoaded({ owner, key, data: matching ? result.data : undefined, error: !result.ok && result.error.kind === "http" && result.error.status === 503
        ? "Operational task links require the installed, verified schema capability." : "Task links unavailable in your current scope." });
      if (!result.ok && (result.error.kind === "stale" || result.error.kind === "http" && result.error.status === 401)) void actions.revalidate();
    });
    return () => abort.abort();
  }, [owner, key, record.requestId, record.branchId, candidates, page, actions]);
  const current = loaded?.owner === owner && loaded.key === key ? loaded : undefined;
  async function link(task: Row) {
    const view = current?.data, abort = lifetime.current;
    if (!view?.canLink || !view.candidates || sending.current || !abort || abort.signal.aborted) return;
    sending.current = true; setBusy(true); setMessage("");
    try {
      const session = await auth.getSession(abort.signal);
      if (abort.signal.aborted) return;
      if (!session.ok || session.data.mustChangePassword || session.data.user.id !== user) {
        setMessage("Session changed. Reload before linking."); void actions.revalidate(); return;
      }
      const result = await http({ method: "POST", url: `/api/material-requests/${view.requestId}/linked-tasks`, expectedStatus: 200,
        csrfToken: session.data.csrfToken, signal: abort.signal, body: { taskId: task.taskId, expectedRowVersion: view.version },
        decode: v => isObject(v) && v.requestId === view.requestId && v.taskId === task.taskId && typeof v.version === "string" ? v : undefined });
      if (abort.signal.aborted) return;
      setMessage(result.ok ? "Task linked. Reload the material request before making other changes."
        : "Link not confirmed. Reload links to check the outcome before trying again.");
      // Clear the old version after every attempted mutation; never retry automatically.
      setLoaded(undefined); setCandidates(false); setPage(1); setRevision(v => v + 1);
      if (!result.ok && result.error.kind === "http" && result.error.status === 401) void actions.revalidate();
    } finally {
      if (!abort.signal.aborted) { sending.current = false; setBusy(false); }
    }
  }
  return <section aria-label="Linked operational tasks">
    <h3>Operational task links</h3>
    <p>Only tasks in your current TASK_VIEW scope are shown. Linking records work attribution; it does not issue materials or change task state.</p>
    {message && <p role="status">{message}</p>}
    {!current ? <p role="status">Loading task links…</p> : !current.data ? <p role="alert">{current.error}</p> : <>
      {current.data.canLink && <button type="button" disabled={busy} onClick={() => { setCandidates(v => !v); setPage(1); }}>
        {candidates ? "Show linked tasks" : "Select open task to link"}</button>}
      {current.data.items.length === 0 && <p>No {candidates ? "eligible open" : "linked"} tasks visible.</p>}
      <ul>{current.data.items.map(task => <li key={task.taskId}>
        <Link to={`/operational-tasks?branchId=${current.data!.branchId}&taskId=${task.taskId}`}>{task.code}</Link>
        {" "}{task.title} — {task.state ?? "Workflow state unavailable"}
        {candidates && current.data!.canLink && <button type="button" disabled={busy} onClick={() => void link(task)}>Link task</button>}
      </li>)}</ul>
      <button type="button" disabled={busy || page <= 1} onClick={() => setPage(p => p - 1)}>Previous</button>
      <button type="button" disabled={busy || page * 25 >= current.data.total} onClick={() => setPage(p => p + 1)}>Next</button>
    </>}
    <button type="button" disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload task links</button>
  </section>;
}
