import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressPanel } from "./TaskProgressPanel";
import {
  readTasks,
  TaskReadFailure,
  type TaskPage,
  type TaskDetail,
} from "./api";

export function TaskReadPage() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.session &&
    !state.session.mustChangePassword &&
    state.pending === null
      ? state.authorityGeneration
      : null;
  const [branch, setBranch] = useState("");
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState<{
    branch: number;
    search: string;
    page: number;
    id: number | null;
    revision: number;
  } | null>(null);
  const [result, setResult] = useState<{
    owner: AuthorityGeneration;
    key: string;
    data?: TaskPage | TaskDetail;
    error?: string;
  } | null>(null);
  const key = JSON.stringify(query);
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
            setQuery({ branch: id, search, page: 1, id: null, revision: 0 });
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
        <button>Load tasks</button>
      </form>
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
                — {task.status}, {task.progressPercent}%
                {task.dueAt && <>; due {task.dueAt}</>}
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
            {detail.task.code} — {detail.task.status},{" "}
            {detail.task.progressPercent}%
          </p>
          <p>{detail.description}</p>
          <p>{detail.completionNote}</p>
          {detail.task.status === "IN_PROGRESS" && (
            <TaskProgressPanel
              key={`${owner}:${detail.task.branchId}:${detail.task.id}`}
              branch={detail.task.branchId}
              task={detail.task.id}
              owner={owner}
            />
          )}
          <h3>Progress history</h3>
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
