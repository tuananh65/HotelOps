import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressError } from "./progress";
import { TaskSubmissionReview } from "./TaskSubmissionReview";
import { readReopen, reopenTask, type ReopenSnapshot } from "./reopen";

export function TaskReopenPanel({
  branch,
  task,
  owner,
  onReopened,
}: {
  branch: number;
  task: number;
  owner: AuthorityGeneration;
  onReopened: () => void;
}) {
  const { actions } = useAuth();
  const [snapshot, setSnapshot] = useState<ReopenSnapshot | null>(null);
  const [assignee, setAssignee] = useState("");
  const [reason, setReason] = useState("");
  const [instructions, setInstructions] = useState("");
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
        ? "The task changed. Reload reopening details before trying again."
        : "Reopening is unavailable or its outcome could not be confirmed. Reload before trying again.",
    );
  }
  useEffect(() => {
    const controller = new AbortController();
    active.current = controller;
    setSnapshot(null);
    setLocked(true);
    setBusy(true);
    setAssignee("");
    setError("");
    setHidden(false);
    void readReopen(branch, task, query.page, query.search, controller.signal)
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
            [403, 404, 503].includes(error.status)
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
    if (!assignee || reason.trim().length < 10 || !instructions.trim()) {
      setError(
        "Select an executor, enter a reason of at least 10 characters and remediation instructions.",
      );
      return;
    }
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    try {
      await reopenTask(
        branch,
        task,
        snapshot,
        Number(assignee),
        reason,
        instructions,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setLocked(true);
        onReopened();
      }
    } catch (error) {
      if (!controller.signal.aborted) failure(error);
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  if (
    hidden ||
    (snapshot && !snapshot.data.canReopen && snapshot.data.history.length === 0)
  )
    return null;
  return (
    <section aria-label="Task reopening">
      <h3>Processing cycles</h3>
      <button
        disabled={busy}
        onClick={() => setQuery((q) => ({ ...q, revision: q.revision + 1 }))}
      >
        Reload reopening details
      </button>
      {error && <p role="alert">{error}</p>}
      {snapshot && (
        <>
          <ol>
            {snapshot.data.history.map((event) => (
              <li key={event.previousSubmissionId}>
                Reopened {event.reopenedAt} by user {event.reopenedBy}; assigned
                to user {event.assigneeId}.<p>Reason: {event.reason}</p>
                <p>Remediation instructions: {event.instructions}</p>
                <TaskSubmissionReview
                  branch={branch}
                  task={task}
                  owner={owner}
                  submission={event.previousSubmissionId}
                />
              </li>
            ))}
          </ol>
          {snapshot.data.canReopen && (
            <>
              <p>
                Review the previous submission and verification history below.
                Reopening preserves these results and starts a new cycle. The
                selected executor must accept again before starting work.
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
                  Find reopening executor
                  <input
                    value={search}
                    maxLength={150}
                    disabled={busy}
                    onChange={(e) => setSearch(e.target.value)}
                  />
                </label>
                <button disabled={busy}>Find reopening executors</button>
              </form>
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  void save();
                }}
              >
                <label>
                  Reopening executor
                  <select
                    aria-label="Reopening executor"
                    required
                    disabled={busy || locked}
                    value={assignee}
                    onChange={(e) => setAssignee(e.target.value)}
                  >
                    <option value="">Select executor</option>
                    {snapshot.data.executors.map((e) => (
                      <option key={e.userId} value={e.userId}>
                        {e.name} — {e.openTasks} open tasks,{" "}
                        {e.loggedSecondsToday}s logged today
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Reopening reason
                  <textarea
                    required
                    minLength={10}
                    maxLength={4000}
                    disabled={busy || locked}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  />
                </label>
                <label>
                  Remediation instructions
                  <textarea
                    required
                    maxLength={4000}
                    disabled={busy || locked}
                    value={instructions}
                    onChange={(e) => setInstructions(e.target.value)}
                  />
                </label>
                <button disabled={busy || locked}>Reopen task</button>
              </form>
              <p>
                Executor page {snapshot.data.page}; {snapshot.data.totalCount}{" "}
                eligible executors. Logged time uses closed intervals for today
                in Asia/Ho_Chi_Minh.
              </p>
              <button
                disabled={busy || query.page <= 1}
                onClick={() => setQuery((q) => ({ ...q, page: q.page - 1 }))}
              >
                Previous reopening executors
              </button>
              <button
                disabled={busy || query.page * 25 >= snapshot.data.totalCount}
                onClick={() => setQuery((q) => ({ ...q, page: q.page + 1 }))}
              >
                Next reopening executors
              </button>
            </>
          )}
        </>
      )}
    </section>
  );
}
