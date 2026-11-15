import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressError } from "./progress";
import {
  assignTask,
  readAssignment,
  type AssignmentSnapshot,
} from "./assignment";

export function TaskAssignmentPanel({
  branch,
  task,
  owner,
  onAssigned,
}: {
  branch: number;
  task: number;
  owner: AuthorityGeneration;
  onAssigned: () => void;
}) {
  const { actions } = useAuth();
  const [snapshot, setSnapshot] = useState<AssignmentSnapshot | null>(null);
  const [assignee, setAssignee] = useState("");
  const [reason, setReason] = useState("");
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState({ search: "", page: 1, revision: 0 });
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(true);
  const [hidden, setHidden] = useState(false);
  const [locked, setLocked] = useState(true);
  const active = useRef<AbortController | null>(null);
  const saving = useRef(false);
  function failure(error: unknown) {
    if (error instanceof TaskProgressError && error.status === 401)
      actions.invalidateSession(owner);
    setLocked(true);
    setError(
      error instanceof TaskProgressError && error.status === 412
        ? "The task changed. Reload assignment and review the current owner."
        : "Assignment is unavailable or its outcome could not be confirmed. Reload before trying again.",
    );
  }
  useEffect(() => {
    const controller = new AbortController();
    active.current = controller;
    setSnapshot(null);
    setLocked(true);
    setBusy(true);
    setAssignee("");
    setReason("");
    setError("");
    setHidden(false);
    void readAssignment(
      branch,
      task,
      query.page,
      query.search,
      controller.signal,
    )
      .then((value) => {
        if (!controller.signal.aborted) {
          setSnapshot(value);
          setLocked(false);
        }
      })
      .catch((error) => {
        if (!controller.signal.aborted) {
          if (
            error instanceof TaskProgressError &&
            [403, 409].includes(error.status)
          )
            setHidden(true);
          else failure(error);
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setBusy(false);
      });
    return () => {
      controller.abort();
      active.current?.abort();
    };
  }, [branch, task, owner, query, actions]);
  async function save() {
    if (!snapshot || busy || locked || saving.current) return;
    if (!assignee || (snapshot.data.assigneeId !== null && !reason.trim())) {
      setError("Select an executor and enter a reason when reassigning.");
      return;
    }
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    try {
      await assignTask(
        branch,
        task,
        snapshot,
        Number(assignee),
        reason,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setLocked(true);
        onAssigned();
      }
    } catch (error) {
      if (!controller.signal.aborted) failure(error);
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  if (hidden) return null;
  return (
    <section aria-label="Task assignment">
      <h3>Assign or reassign task</h3>
      <button
        disabled={busy}
        onClick={() => setQuery((q) => ({ ...q, revision: q.revision + 1 }))}
      >
        Reload assignment
      </button>
      {error && <p role="alert">{error}</p>}
      {snapshot && (
        <>
          <p>
            Workflow: {snapshot.data.state}; current executor:{" "}
            {snapshot.data.assigneeId ?? "Unassigned"}
          </p>
          <p>
            Workload in this branch as of {snapshot.data.asOf}. Logged time
            today ({snapshot.data.timezone}) includes closed work intervals;
            paused time is excluded.
          </p>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              if (!busy)
                setQuery((q) => ({
                  search,
                  page: 1,
                  revision: q.revision + 1,
                }));
            }}
          >
            <label>
              Find executor
              <input
                maxLength={150}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </label>
            <button disabled={busy}>Find executor</button>
          </form>
          <fieldset disabled={busy || locked}>
            <legend>Select an eligible executor</legend>
            <label>
              Executor
              <select
                aria-label="Executor"
                value={assignee}
                onChange={(e) => setAssignee(e.target.value)}
              >
                <option value="">Choose executor</option>
                {snapshot.data.executors.map((e) => (
                  <option
                    key={e.userId}
                    value={e.userId}
                    disabled={e.userId === snapshot.data.assigneeId}
                  >
                    {e.name} — {e.openTasks} open tasks; {e.loggedSecondsToday}{" "}
                    seconds logged today
                  </option>
                ))}
              </select>
            </label>
            <label>
              Reassignment reason
              <textarea
                maxLength={4000}
                required={snapshot.data.assigneeId !== null}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
            <button onClick={() => void save()}>
              {snapshot.data.assigneeId === null
                ? "Assign task"
                : "Reassign task"}
            </button>
          </fieldset>
          <nav aria-label="Executor pagination">
            <button
              disabled={busy || query.page === 1}
              onClick={() => setQuery((q) => ({ ...q, page: q.page - 1 }))}
            >
              Previous executors
            </button>
            <span>
              Page {query.page}; {snapshot.data.totalCount} eligible executors
            </span>
            <button
              disabled={busy || query.page * 25 >= snapshot.data.totalCount}
              onClick={() => setQuery((q) => ({ ...q, page: q.page + 1 }))}
            >
              Next executors
            </button>
          </nav>
        </>
      )}
    </section>
  );
}
