import { userMessage } from "../i18n/userMessage";
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
        || typeof v.version !== "string" || !/^[A-Za-z0-9+/]{11}=$/.test(v.version)) throw new Error("Thông tin điền sẵn của yêu cầu không hợp lệ");
      if (!abort.signal.aborted) setLoaded({ owner, id, branch, source: v });
    }).catch(e => { if (!abort.signal.aborted) { setMessage(e instanceof TaskProgressError && e.status === 403
      ? "MSG02: Tạo công việc cần quyền quản lý công việc và thẩm quyền yêu cầu hiện tại. Liên kết công việc sẵn có hoặc nhờ người quản lý."
      : "Chưa có thông tin điền sẵn. Tải lại để xác minh quyền và trạng thái."); if (e instanceof TaskProgressError && e.status === 401) actions.invalidateSession(owner!); } });
    return () => abort.abort();
  }, [id, branch, owner, revision, actions]);
  if (!owner) return null;
  const source = loaded?.owner === owner && loaded.id === id && loaded.branch === branch ? loaded.source : null;
  return <section aria-label="Tạo công việc từ yêu cầu">
    {message && <p role="status">{userMessage(message)}</p>}
    {created?.owner === owner && <p role="status">Đã tạo và liên kết công việc. <Link to={`/operational-tasks?branchId=${branch}&taskId=${created.task}`}>Mở công việc vừa tạo</Link>. Reload linked tasks to view the relationship.</p>}
    {!source && !created && <button onClick={() => setRevision(v => v + 1)}>Tải lại thông tin điền sẵn của yêu cầu</button>}
    {source && <TaskCreatePanel key={`${owner}:${id}:${revision}`} branch={branch} owner={owner} source={source}
      onSourceInvalid={() => { setLoaded(null); setMessage("Quyền hoặc phiên bản yêu cầu đã đổi. Tải lại thông tin điền sẵn trước khi thử lại."); }}
      onSeriesCreated={() => { setLoaded(null); setMessage("MSG08: Đã tạo chuỗi định kỳ. Công việc bắt buộc đã tạo sẽ chặn hoàn tất; lần tương lai chưa lên lịch không chặn. Yêu cầu kết thúc sẽ dừng tạo; mở lại không tự tạo tiếp."); onLinked?.(); navigate(`/operational-tasks?branchId=${branch}`); }}
      onCreated={(task, assign) => { setLoaded(null); setCreated({ owner, task }); onLinked?.(); if (onCreated) onCreated(task, assign); else if (assign) navigate(`/operational-tasks?branchId=${branch}&taskId=${task}`); }} />}
  </section>;
}
