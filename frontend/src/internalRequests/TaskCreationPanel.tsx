import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { TaskCreatePanel } from "../operationalTasks/TaskCreatePanel";
import { planningRequest, type TaskRequestContext } from "../operationalTasks/planning";
import { TaskProgressError } from "../operationalTasks/progress";

export function InternalRequestTaskCreationPanel({ id, branch, onCreated, onLinked }: { id: number; branch: number; onCreated?: (task: number, assign: boolean) => void; onLinked?: () => void }) {
  const { state, actions } = useAuth(); const navigate = useNavigate();
  const owner = state.status === "AUTHENTICATED" && state.pending === null && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{ owner: typeof owner; id: number; branch: number; source: TaskRequestContext } | null>(null);
  const [message, setMessage] = useState(""); const [created, setCreated] = useState<{ owner: typeof owner; task: number } | null>(null);
  useEffect(() => {
    const abort = new AbortController(); setLoaded(null); setMessage(""); setCreated(null);
    if (owner) void planningRequest(`/api/branches/${branch}/operational-tasks/request-context/${id}`, abort.signal).then(r => r.json()).then((v: TaskRequestContext) => {
      const positive = (n: unknown): n is number => typeof n === "number" && Number.isSafeInteger(n) && n > 0;
      if (!v || v.requestId !== id || v.branchId !== branch || !positive(v.departmentId) || typeof v.title !== "string" || typeof v.description !== "string"
        || ![v.roomId, v.areaId, v.priorityId].every(n => n === null || positive(n)) || v.roomId !== null && v.areaId !== null
        || !(v.dueAt === null || typeof v.dueAt === "string" && /(?:Z|\+00:00)$/.test(v.dueAt) && Number.isFinite(Date.parse(v.dueAt)))
        || typeof v.version !== "string" || !/^[A-Za-z0-9+/]{11}=$/.test(v.version)) throw new Error("Invalid request prefill");
      if (!abort.signal.aborted) setLoaded({ owner, id, branch, source: v });
    }).catch(e => { if (!abort.signal.aborted) { setMessage(e instanceof TaskProgressError && e.status === 403
      ? "MSG02: Creating this work requires task management and current request authority. Link existing work or ask the responsible manager."
      : "Request prefill unavailable. Reload to verify current access and state."); if (e instanceof TaskProgressError && e.status === 401) actions.invalidateSession(owner!); } });
    return () => abort.abort();
  }, [id, branch, owner, revision, actions]);
  if (!owner) return null;
  const source = loaded?.owner === owner && loaded.id === id && loaded.branch === branch ? loaded.source : null;
  return <section aria-label="Create task from request">
    {message && <p role="status">{message}</p>}
    {created?.owner === owner && <p role="status">MSG08: Task created and linked. <Link to={`/operational-tasks?branchId=${branch}&taskId=${created.task}`}>Open created task</Link>. Reload linked tasks to view the relationship.</p>}
    {!source && !created && <button onClick={() => setRevision(v => v + 1)}>Reload request prefill</button>}
    {source && <TaskCreatePanel key={`${owner}:${id}:${revision}`} branch={branch} owner={owner} source={source}
      onSourceInvalid={() => { setLoaded(null); setMessage("Request authority or version changed. Reload request prefill before trying again."); }}
      onSeriesCreated={() => { setLoaded(null); setMessage("MSG08: Recurring series created. Generated mandatory tasks block completion; future unscheduled occurrences do not. Terminal requests stop generation; Reopen does not restart it."); onLinked?.(); navigate(`/operational-tasks?branchId=${branch}`); }}
      onCreated={(task, assign) => { setLoaded(null); setCreated({ owner, task }); onLinked?.(); if (onCreated) onCreated(task, assign); else if (assign) navigate(`/operational-tasks?branchId=${branch}&taskId=${task}`); }} />}
  </section>;
}
