import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressError } from "./progress";
import {
  changeTaskPlan,
  creationOptions,
  localPlanTime,
  readTaskPlan,
  utcPlanTime,
  type PlanFields,
  type PlanSnapshot,
  type PlanningOptions,
} from "./planning";
import { TaskPlanFields, type PlanDraft } from "./TaskPlanFields";
import { TaskSeriesPanel } from "./TaskSeriesPanel";

export function TaskPlanPanel({
  branch,
  task,
  owner,
  onChanged,
}: {
  branch: number;
  task: number;
  owner: AuthorityGeneration;
  onChanged: () => void;
}) {
  const { actions } = useAuth();
  const [snapshot, setSnapshot] = useState<PlanSnapshot | null>(null);
  const [priorities, setPriorities] = useState<PlanningOptions["priorities"]>(
    [],
  );
  const [plan, setPlan] = useState<PlanDraft>({
    priority: "",
    start: "",
    due: "",
    minutes: "",
  });
  const [reason, setReason] = useState("");
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [query, setQuery] = useState({ page: 1, revision: 0 });
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(true);
  const [locked, setLocked] = useState(true);
  const [hidden, setHidden] = useState(false);
  const saving = useRef(false);
  const active = useRef<AbortController | null>(null);
  function failure(error: unknown) {
    if (error instanceof TaskProgressError && error.status === 401)
      actions.invalidateSession(owner);
    setLocked(true);
    setError(
      error instanceof TaskProgressError && error.status === 412
        ? "The task changed. Reload its current plan before continuing."
        : "The task plan or change outcome is unavailable. Reload and review before continuing.",
    );
  }
  useEffect(() => {
    const controller = new AbortController();
    active.current = controller;
    setSnapshot(null);
    setPriorities([]);
    setLocked(true);
    setBusy(true);
    setHidden(false);
    setError("");
    setReason("");
    setConfirmCancel(false);
    void readTaskPlan(branch, task, query.page, controller.signal)
      .then(async (value) => {
        const options = value.data.canManage
          ? await creationOptions(branch, null, controller.signal)
          : null;
        if (controller.signal.aborted) return;
        setSnapshot(value);
        setPriorities(options?.priorities ?? []);
        setPlan({
          priority: value.data.priorityId?.toString() ?? "",
          start: localPlanTime(value.data.plannedStartAt),
          due: localPlanTime(value.data.dueAt),
          minutes: value.data.slaMinutes?.toString() ?? "",
        });
        setLocked(false);
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        if (
          error instanceof TaskProgressError &&
          [403, 404, 409].includes(error.status)
        )
          setHidden(true);
        else failure(error);
      })
      .finally(() => {
        if (!controller.signal.aborted) setBusy(false);
      });
    return () => {
      controller.abort();
      active.current?.abort();
    };
  }, [branch, task, owner, query, actions]);
  async function save(cancel: boolean) {
    if (!snapshot || busy || locked || saving.current) return;
    if (cancel && (!confirmCancel || reason.trim().length < 10)) {
      setError(
        "Confirm cancellation and provide a reason of at least 10 characters.",
      );
      return;
    }
    if (
      !cancel &&
      snapshot.data.state !== "PENDING_ASSIGNMENT" &&
      !reason.trim()
    ) {
      setError("Changing an assigned task requires a reason.");
      return;
    }
    let input: PlanFields | null;
    try {
      input = cancel
        ? null
        : {
            priorityId: Number(plan.priority),
            plannedStartAt: utcPlanTime(
              plan.start,
              snapshot.data.plannedStartAt,
            ),
            dueAt: utcPlanTime(plan.due, snapshot.data.dueAt),
            slaMinutes: plan.minutes ? Number(plan.minutes) : null,
          };
      if (
        input &&
        (!priorities.some((p) => p.id === Number(plan.priority)) ||
          (input.plannedStartAt &&
            input.dueAt &&
            Date.parse(input.dueAt) < Date.parse(input.plannedStartAt)))
      ) {
        setError(
          "Select an active priority and a deadline at or after planned start.",
        );
        return;
      }
    } catch {
      setError("Check planning dates before saving.");
      return;
    }
    saving.current = true;
    setBusy(true);
    setError("");
    const controller = new AbortController();
    active.current = controller;
    try {
      await changeTaskPlan(
        branch,
        task,
        snapshot,
        input,
        reason,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setLocked(true);
        onChanged();
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
    <section aria-label="Task plan">
      <h3>Task plan and deadline history</h3>
      <button
        disabled={busy}
        onClick={() => setQuery((q) => ({ ...q, revision: q.revision + 1 }))}
      >
        Reload task plan
      </button>
      {error && <p role="alert">{error}</p>}
      {snapshot && (
        <>
          <p>
            Expected outcome: {snapshot.data.requestedResult ?? "Not recorded"}
          </p>
          <p>
            Planned start: {snapshot.data.plannedStartAt ?? "Not set"};
            deadline: {snapshot.data.dueAt ?? "Not set"}; expected duration:{" "}
            {snapshot.data.slaMinutes ?? "Not set"} minutes.
          </p>
          {snapshot.data.canManage && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                void save(false);
              }}
            >
              <fieldset disabled={busy || locked}>
                <legend>Update task plan</legend>
                <TaskPlanFields
                  value={plan}
                  onChange={setPlan}
                  priorities={priorities}
                />
                <label>
                  Plan change or cancellation reason
                  <textarea
                    maxLength={4000}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  />
                </label>
                <button>Save task plan</button>
                {snapshot.data.canCancel && (
                  <>
                    <label>
                      <input
                        type="checkbox"
                        checked={confirmCancel}
                        onChange={(e) => setConfirmCancel(e.target.checked)}
                      />
                      MSG33: Confirm cancellation of this task
                    </label>
                    <button
                      type="button"
                      disabled={!confirmCancel || reason.trim().length < 10}
                      onClick={() => void save(true)}
                    >
                      Cancel task
                    </button>
                  </>
                )}
              </fieldset>
            </form>
          )}
          {snapshot.data.seriesId && (
            <TaskSeriesPanel
              key={`series:${owner}:${branch}:${snapshot.data.seriesId}`}
              branch={branch}
              owner={owner}
              seriesId={snapshot.data.seriesId}
              taskId={task}
            />
          )}
          <ol aria-label="Task plan history">
            {snapshot.data.history.map((h) => (
              <li key={h.auditId}>
                {h.at}: deadline {h.previousDueAt ?? "Not set"} →{" "}
                {h.dueAt ?? "Not set"}; priority{" "}
                {h.previousPriorityId ?? "Not set"} →{" "}
                {h.priorityId ?? "Not set"}; expected duration{" "}
                {h.previousSlaMinutes ?? "Not set"} →{" "}
                {h.slaMinutes ?? "Not set"} minutes. {h.reason}
              </li>
            ))}
          </ol>
          <nav aria-label="Plan history pagination">
            <button
              disabled={busy || query.page === 1}
              onClick={() => setQuery((q) => ({ ...q, page: q.page - 1 }))}
            >
              Previous plan changes
            </button>
            <span>
              Page {query.page}; {snapshot.data.historyTotalCount} changes
            </span>
            <button
              disabled={
                busy || query.page * 25 >= snapshot.data.historyTotalCount
              }
              onClick={() => setQuery((q) => ({ ...q, page: q.page + 1 }))}
            >
              Next plan changes
            </button>
          </nav>
        </>
      )}
    </section>
  );
}
