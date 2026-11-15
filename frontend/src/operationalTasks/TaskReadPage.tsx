import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressPanel } from "./TaskProgressPanel";
import { TaskCriteriaSetup } from "./TaskCriteriaSetup";
import { TaskSubmissionReview } from "./TaskSubmissionReview";
import { TaskVerificationPanel } from "./TaskVerificationPanel";
import { TaskAssignmentPanel } from "./TaskAssignmentPanel";
import {
  readTasks,
  taskWorkflowStates,
  type TaskWorkflowState,
  TaskReadFailure,
  type TaskPage,
  type TaskDetail,
} from "./api";

export function TaskReadPage() {
  const { state, actions } = useAuth();
  const [params] = useSearchParams();
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
  const [progressRevision, setProgressRevision] = useState(0);
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
  useEffect(() => {
    const sourceBranch = Number(params.get("branchId"));
    const sourceTask = Number(params.get("taskId"));
    if (
      !owner ||
      !Number.isSafeInteger(sourceBranch) ||
      sourceBranch <= 0 ||
      !Number.isSafeInteger(sourceTask) ||
      sourceTask <= 0
    )
      return;
    setBranch(String(sourceBranch));
    setQuery({
      branch: sourceBranch,
      search: "",
      page: 1,
      id: sourceTask,
      revision: 0,
    });
  }, [params, owner]);
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
              ? "You do not have access to tasks in this branch."
              : "Tasks are unavailable. Refresh to try again.",
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
      <p role="status">Task data is hidden while your session is checked.</p>
    );
  return (
    <main>
      <h1>Operational tasks</h1>
      <Link to="/">Home</Link>
      <form
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
          Branch ID
          <input
            required
            inputMode="numeric"
            pattern="[1-9][0-9]*"
            value={branch}
            onChange={(e) => setBranch(e.target.value)}
          />
        </label>
        <label>
          Search tasks
          <input
            maxLength={150}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <label>
          Workflow status
          <select
            aria-label="Workflow status"
            value={workflowStatus}
            onChange={(e) =>
              setWorkflowStatus(e.target.value as TaskWorkflowState | "")
            }
          >
            <option value="">All workflows</option>
            {taskWorkflowStates.map((s) => (
              <option key={s} value={s}>
                {s.replaceAll("_", " ")}
              </option>
            ))}
          </select>
        </label>
        <button>Load tasks</button>
      </form>
      {declined === owner && (
        <p role="status">
          MSG19: Task declined and returned to Pending Assignment.
        </p>
      )}
      {assigned === owner && (
        <p role="status">
          MSG08: Task assigned. The new executor must accept before starting.
        </p>
      )}
      {query && (
        <button
          onClick={() => setQuery({ ...query, revision: query.revision + 1 })}
        >
          Refresh
        </button>
      )}
      {visible?.error && <p role="alert">{visible.error}</p>}
      {query && !visible && <p role="status">Loading tasks…</p>}
      {page && (
        <>
          <p>{page.totalCount} tasks</p>
          <ul>
            {page.items.map((task) => (
              <li key={task.id}>
                <button
                  onClick={() =>
                    query && setQuery({ ...query, id: task.id, page: 1 })
                  }
                >
                  {task.code}: {task.title}
                </button>{" "}
                — {task.workflowStatus ?? "Workflow unavailable"},{" "}
                {task.progressPercent}%{task.dueAt && <>; due {task.dueAt}</>}
              </li>
            ))}
          </ul>
        </>
      )}
      {detail && (
        <section>
          <button
            onClick={() => query && setQuery({ ...query, id: null, page: 1 })}
          >
            Back to tasks
          </button>
          <h2>{detail.task.title}</h2>
          <p>
            {detail.task.code} —{" "}
            {detail.task.workflowStatus ?? "Workflow unavailable"},{" "}
            {detail.task.progressPercent}%
          </p>
          <p>{detail.description}</p>
          <p>
            Priority: {detail.priority ?? "Not specified"}; deadline:{" "}
            {detail.task.dueAt ?? "Not specified"}
          </p>
          <p>{detail.completionNote}</p>
          <TaskAssignmentPanel
            key={`assign:${owner}:${detail.task.branchId}:${detail.task.id}:${query?.revision}`}
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
              onDeclined={() => {
                setDeclined(owner);
                setResult(null);
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
          <h3>Progress history</h3>
          <TaskSubmissionReview
            key={`submission:${owner}:${detail.task.branchId}:${detail.task.id}`}
            branch={detail.task.branchId}
            task={detail.task.id}
            owner={owner}
          />
          <TaskVerificationPanel
            key={`verify:${owner}:${detail.task.branchId}:${detail.task.id}`}
            branch={detail.task.branchId}
            task={detail.task.id}
            owner={owner}
          />
          <p>{detail.progressTotalCount} recorded updates</p>
          <ul>
            {detail.progress.map((p) => (
              <li key={p.id}>
                {p.reportedAt}: {p.fromStatus} → {p.toStatus},{" "}
                {p.progressPercent}%<p>{p.description}</p>
                {p.blockerReason && <p>Blocker: {p.blockerReason}</p>}
              </li>
            ))}
          </ul>
        </section>
      )}
      {data && query && (
        <nav aria-label="Task pagination">
          <button
            disabled={query.page === 1}
            onClick={() => setQuery({ ...query, page: query.page - 1 })}
          >
            Previous
          </button>
          <span>Page {query.page}</span>
          <button
            disabled={
              query.page * 25 >=
              (page?.totalCount ?? detail?.progressTotalCount ?? 0)
            }
            onClick={() => setQuery({ ...query, page: query.page + 1 })}
          >
            Next
          </button>
        </nav>
      )}
    </main>
  );
}
