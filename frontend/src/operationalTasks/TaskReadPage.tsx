import { useEffect, useState } from "react";
import { useTaskBranches } from "./useTaskBranches";
import { displayCode } from "../i18n/vi";
import { staffDate } from "../internalRequests/staffDate";
import { LoadingIndicator } from "../components/LoadingIndicator";
import "../internalRequests/staff-workspace.css";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressPanel } from "./TaskProgressPanel";
import { TaskCriteriaSetup } from "./TaskCriteriaSetup";
import { TaskVerificationPanel } from "./TaskVerificationPanel";
import { TaskAssignmentPanel } from "./TaskAssignmentPanel";
import { TaskCreationHost } from "./TaskCreationHost";
import { TaskSeriesPanel } from "./TaskSeriesPanel";
import { TaskPlanPanel } from "./TaskPlanPanel";
import { TaskReopenPanel } from "./TaskReopenPanel";
import { TaskWorkloadPanel } from "./TaskWorkloadPanel";
import { dashboardWorkloadBranches, dashboardWorkloadFilters } from "../insights/dashboardWorkloadLink";
import {
  readTasks,
  taskWorkflowStates,
  type TaskWorkflowState,
  TaskReadFailure,
  type TaskPage,
  type TaskDetail,
} from "./api";

export function TaskReadPage() {
  const branches = useTaskBranches();
  const { state, actions } = useAuth();
  const [params, setParams] = useSearchParams();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.session &&
    !state.session.mustChangePassword &&
    state.pending === null
      ? state.authorityGeneration
      : null;
  const [branch, setBranch] = useState("");
  const [search, setSearch] = useState("");
  const [workflowStatus, setWorkflowStatus] = useState<TaskWorkflowState | "">(
    "",
  );
  const [declined, setDeclined] = useState<AuthorityGeneration | null>(null);
  const [assigned, setAssigned] = useState<AuthorityGeneration | null>(null);
  const [planned, setPlanned] = useState<AuthorityGeneration | null>(null);
  const [reopened, setReopened] = useState<AuthorityGeneration | null>(null);
  const [progressRevision, setProgressRevision] = useState(0);
  const [workflowNotice, setWorkflowNotice] = useState<{ owner: AuthorityGeneration; branch: number; task: number; message: string } | null>(null);
  const [query, setQuery] = useState<{
    branch: number;
    search: string;
    page: number;
    id: number | null;
    revision: number;
    workflowStatus?: TaskWorkflowState;
  } | null>(null);
  const [result, setResult] = useState<{
    owner: AuthorityGeneration;
    key: string;
    data?: TaskPage | TaskDetail;
    error?: string;
  } | null>(null);
  const key = JSON.stringify(query);
  function workflowChanged(message: string) {
    if (!owner || !query?.id) return;
    setWorkflowNotice({ owner, branch: query.branch, task: query.id, message });
    setResult(null);
    setProgressRevision(n => n + 1);
    setQuery({ ...query, revision: query.revision + 1 });
  }
  const workloadBranches = dashboardWorkloadBranches(params);
  useEffect(() => {
    if (params.has("workloadBranches")) { setQuery(null); return; }
    if (!branches.items) { setQuery(null); return; }
    const sourceBranch = params.has("branchId") ? Number(params.get("branchId"))
      : (branches.items.find(b => b.isPrimary) ?? (branches.items.length === 1 ? branches.items[0] : undefined))?.id;
    const sourceTask = params.has("taskId")
      ? Number(params.get("taskId"))
      : null;
    if (
      !owner ||
      !Number.isSafeInteger(sourceBranch) ||
      !sourceBranch || sourceBranch <= 0 || !branches.items.some(b => b.id === sourceBranch) ||
      (sourceTask !== null &&
        (!Number.isSafeInteger(sourceTask) || sourceTask <= 0))
    ) {
      setQuery(null);
      return;
    }
    setBranch(String(sourceBranch));
    setQuery((current) => ({
      branch: sourceBranch,
      search: current?.branch === sourceBranch ? current.search : "",
      workflowStatus:
        current?.branch === sourceBranch ? current.workflowStatus : undefined,
      page: 1,
      id: sourceTask,
      revision: 0,
    }));
  }, [params, owner, branches.items]);
  useEffect(() => {
    if (!owner || !query) return;
    const controller = new AbortController();
    setResult(null);
    void readTasks(
      query.branch,
      query.page,
      query.search,
      query.id,
      controller.signal,
      query.workflowStatus,
    )
      .then((data) => {
        if (!controller.signal.aborted) setResult({ owner, key, data });
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        setResult({
          owner,
          key,
          error:
            error instanceof TaskReadFailure && error.status === 403
              ? "Bạn không có quyền xem công việc trong chi nhánh này."
              : "Chưa tải được công việc. Tải lại để thử tiếp.",
        });
        if (error instanceof TaskReadFailure && error.status === 401)
          actions.invalidateSession(owner);
      });
    return () => controller.abort();
  }, [owner, key, query, actions]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  const data = visible?.data;
  const page = data && "items" in data ? data : null;
  const detail = data && "task" in data ? data : null;
  if (!owner)
    return (
      <LoadingIndicator label="Đang kiểm tra phiên đăng nhập" />
    );
  if (params.has("workloadBranches")) return (
    <main aria-label="Khối lượng công việc theo các chi nhánh">
      <h1>Khối lượng công việc tổng quan</h1>
      <Link to="/dashboard">Quay lại tổng quan</Link>
      {!workloadBranches ? <p role="alert">Phạm vi khối lượng công việc không hợp lệ. Quay lại tổng quan và chọn lại bộ lọc.</p> : <>
        <p>Chi nhánh: {workloadBranches.join(", ")}. Each branch shows its own current snapshot and pagination. Access is checked again for every branch.</p>
        {workloadBranches.map(id => <section key={`${owner}:${id}:${params.toString()}`} aria-label={`Chi nhánh theo dõi khối lượng ${id}`}>
          <h2>Chi nhánh {id}</h2>
          <TaskWorkloadPanel branch={id} owner={owner} initialFilters={dashboardWorkloadFilters(params)} />
        </section>)}
      </>}
    </main>
  );
  return (
    <main className="staff-workspace">
      <header className="staff-heading"><div><h1>Công việc được giao</h1>
      <p className="staff-subtitle">Theo dõi công việc, tiến độ và thời hạn trong chi nhánh của bạn.</p></div><Link to="/home">Trang chủ</Link></header>
      {branches.error && <p role="alert" className="staff-notice">{branches.error} <button onClick={branches.reload}>Thử lại</button></p>}
      {!branches.items && !branches.error && <LoadingIndicator label="Đang tải chi nhánh" />}
      {branches.items?.length === 0 && <p className="staff-empty">Bạn chưa có chi nhánh được phép xem công việc.</p>}
      <form
        className="staff-filters"
        onSubmit={(e) => {
          e.preventDefault();
          const id = Number(branch);
          if (/^[1-9]\d*$/.test(branch) && Number.isSafeInteger(id))
            setQuery({
              branch: id,
              search,
              page: 1,
              id: null,
              revision: 0,
              workflowStatus: workflowStatus || undefined,
            });
        }}
      >
        <label>
          Chi nhánh
          {branches.items?.length === 1 ? <span className="staff-branch">{branches.items[0].name}</span> :
          <select aria-label="Chi nhánh" required value={branch} onChange={e => { setBranch(e.target.value); setParams({branchId:e.target.value}); }}>
            <option value="">Chọn chi nhánh</option>{branches.items?.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
          </select>}
        </label>
        <label>
          Tìm công việc
          <input
            maxLength={150}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <label>
          Trạng thái quy trình
          <select
            aria-label="Trạng thái quy trình"
            value={workflowStatus}
            onChange={(e) =>
              setWorkflowStatus(e.target.value as TaskWorkflowState | "")
            }
          >
            <option value="">Tất cả quy trình</option>
            {taskWorkflowStates.map((s) => (
              <option key={s} value={s}>
                {displayCode(s)}
              </option>
            ))}
          </select>
        </label>
        <button disabled={!branch || !branches.items?.some(b => String(b.id) === branch)}>Tìm kiếm / tải lại</button>
      </form>
      {query && data && (
        <TaskWorkloadPanel
          key={`${owner}:${query.branch}:${query.id ?? "list"}:${params.toString()}`}
          branch={query.branch}
          owner={owner}
          initialSearch={detail?.task.code ?? ""}
          initialFilters={dashboardWorkloadFilters(params)}
        />
      )}
      {declined === owner && (
        <p role="status">
          MSG19: Đã từ chối công việc và trả về chờ phân công.
        </p>
      )}
      {workflowNotice?.owner === owner && workflowNotice.branch === query?.branch && workflowNotice.task === query.id && <p role="status">{workflowNotice.message}</p>}
      {assigned === owner && (
        <p role="status">
          MSG08: Đã phân công công việc. Người thực hiện mới cần tiếp nhận trước khi bắt đầu.
        </p>
      )}
      {reopened === owner && (
        <p role="status">
          MSG08: Đã mở lại công việc. Người thực hiện cần tiếp nhận chu kỳ mới.
        </p>
      )}
      {query && (
        <button
          onClick={() => setQuery({ ...query, revision: query.revision + 1 })}
        >
          Tải lại
        </button>
      )}
      {visible?.error && <p role="alert">{visible.error}</p>}
      {planned === owner && (
        <p role="status">MSG08: Đã lưu định nghĩa hoặc kế hoạch công việc.</p>
      )}
      {page && query && branches.items?.find(b => b.id === query.branch)?.canCreate && (
        <TaskCreationHost
          key={`Tạo mới:${owner}:${query.branch}:${query.revision}`}
          branch={query.branch}
          owner={owner}
          onSeriesCreated={() => {
            setPlanned(owner);
            setResult(null);
            setQuery({
              ...query,
              id: null,
              page: 1,
              revision: query.revision + 1,
            });
          }}
          onCreated={(id, assign) => {
            setPlanned(owner);
            setResult(null);
            if (assign)
              setParams({ branchId: String(query.branch), taskId: String(id) });
            else
              setQuery({
                ...query,
                id: null,
                page: 1,
                revision: query.revision + 1,
              });
          }}
        />
      )}
      {query && !visible && <LoadingIndicator label="Đang tải công việc" />}
      {page && query && (
        <TaskSeriesPanel
          key={`Chuỗi định kỳ:${owner}:${query.branch}:${query.revision}`}
          branch={query.branch}
          owner={owner}
        />
      )}
      {page && (
        <>
          <p>{page.totalCount} công việc</p>
          {page.totalCount === 0 && <p className="staff-empty">Chưa có công việc phù hợp với quyền và bộ lọc hiện tại.</p>}
          <ul className="staff-task-list">
            {page.items.map((task) => (
              <li key={task.id}>
                <button
                  onClick={() =>
                    query &&
                    setParams({
                      branchId: String(query.branch),
                      taskId: String(task.id),
                    })
                  }
                >
                  {task.code}: {task.title}
                </button>{" "}
                <div><span className="staff-status">{displayCode(task.workflowStatus ?? task.status)}</span>{" · "}
                Tiến độ {task.progressPercent}%{task.dueAt && <> · Hạn: {staffDate(task.dueAt)}</>}</div>
              </li>
            ))}
          </ul>
        </>
      )}
      {detail && (
        <section>
          <button
            onClick={() =>
              query && setParams({ branchId: String(query.branch) })
            }
          >
            Quay lại công việc
          </button>
          <h2>{detail.task.title}</h2>
          <p>
            {detail.task.code} —{" "}
            {displayCode(detail.task.workflowStatus ?? detail.task.status)},{" "}
            {detail.task.progressPercent}%
          </p>
          <p>{detail.description}</p>
          <p>
            Độ ưu tiên: {detail.priority ?? "Chưa chỉ định"}; Thời hạn:{" "}
            {staffDate(detail.task.dueAt)}
          </p>
          <p>{detail.completionNote}</p>
          <TaskPlanPanel
            key={`Kế hoạch:${owner}:${detail.task.branchId}:${detail.task.id}:${query?.revision}`}
            branch={detail.task.branchId}
            task={detail.task.id}
            owner={owner}
            onChanged={() => {
              setPlanned(owner);
              setResult(null);
              setProgressRevision((n) => n + 1);
              if (query) setQuery({ ...query, revision: query.revision + 1 });
            }}
          />
          <TaskAssignmentPanel
            key={`Phân công:${owner}:${detail.task.branchId}:${detail.task.id}:${query?.revision}`}
            branch={detail.task.branchId}
            task={detail.task.id}
            owner={owner}
            onAssigned={() => {
              setAssigned(owner);
              setResult(null);
              setProgressRevision((n) => n + 1);
              if (query) setQuery({ ...query, revision: query.revision + 1 });
            }}
          />
          <TaskReopenPanel
            key={`Mở lại:${owner}:${detail.task.branchId}:${detail.task.id}:${query?.revision}`}
            branch={detail.task.branchId}
            task={detail.task.id}
            owner={owner}
            onReopened={() => {
              setReopened(owner);
              setResult(null);
              setProgressRevision((n) => n + 1);
              if (query) setQuery({ ...query, revision: query.revision + 1 });
            }}
          />
          {detail.task.status === "IN_PROGRESS" && (
            <TaskCriteriaSetup
              key={`setup:${owner}:${detail.task.branchId}:${detail.task.id}`}
              branch={detail.task.branchId}
              task={detail.task.id}
              owner={owner}
              onInitialized={() => setProgressRevision((n) => n + 1)}
            />
          )}
          {["TODO", "IN_PROGRESS"].includes(detail.task.status) && (
            <TaskProgressPanel
              key={`${owner}:${detail.task.branchId}:${detail.task.id}:${progressRevision}`}
              branch={detail.task.branchId}
              task={detail.task.id}
              owner={owner}
              onChanged={workflowChanged}
              onDeclined={() => {
                setDeclined(owner);
                setResult(null);
                if (query) setParams({ branchId: String(query.branch) });
                if (query)
                  setQuery({
                    ...query,
                    id: null,
                    page: 1,
                    revision: query.revision + 1,
                  });
              }}
            />
          )}
          <h3>Lịch sử tiến độ</h3>
          <TaskVerificationPanel
            key={`verify:${owner}:${detail.task.branchId}:${detail.task.id}`}
            branch={detail.task.branchId}
            task={detail.task.id}
            owner={owner}
            onChanged={workflowChanged}
          />
          <p>{detail.progressTotalCount} cập nhật đã ghi nhận</p>
          <ul>
            {detail.progress.map((p) => (
              <li key={p.id}>
                {staffDate(p.reportedAt)}: {displayCode(p.fromStatus ?? "")} → {displayCode(p.toStatus ?? "")},{" "}
                {p.progressPercent}%<p>{p.description}</p>
                {p.blockerReason && <p>Trở ngại: {p.blockerReason}</p>}
              </li>
            ))}
          </ul>
        </section>
      )}
      {data && query && (
        <nav aria-label="Phân trang công việc">
          <button
            disabled={query.page === 1}
            onClick={() => setQuery({ ...query, page: query.page - 1 })}
          >
            Trước
          </button>
          <span>Trang {query.page}</span>
          <button
            disabled={
              query.page * 25 >=
              (page?.totalCount ?? detail?.progressTotalCount ?? 0)
            }
            onClick={() => setQuery({ ...query, page: query.page + 1 })}
          >
            Tiếp
          </button>
        </nav>
      )}
    </main>
  );
}
