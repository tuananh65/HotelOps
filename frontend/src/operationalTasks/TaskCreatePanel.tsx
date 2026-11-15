import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressError } from "./progress";
import {
  createTask,
  creationOptions,
  utcPlanTime,
  validateTaskCreation,
  type CreateTaskInput,
  type PlanningOptions,
  type TaskRequestContext,
  localPlanTime,
} from "./planning";
import { TaskPlanFields, type PlanDraft } from "./TaskPlanFields";
import {
  createSeries,
  validateRecurrence,
  type RecurrenceInput,
} from "./series";
import { TaskRecurrenceFields } from "./TaskRecurrenceFields";

export function TaskCreatePanel({
  branch,
  owner,
  onCreated,
  onSeriesCreated,
  source,
  onSourceInvalid,
}: {
  branch: number;
  owner: AuthorityGeneration;
  onCreated: (task: number, assign: boolean) => void;
  onSeriesCreated: () => void;
  source?: TaskRequestContext;
  onSourceInvalid?: () => void;
}) {
  const { actions } = useAuth();
  const [department, setDepartment] = useState(source ? String(source.departmentId) : "");
  const [category, setCategory] = useState("");
  const [options, setOptions] = useState<PlanningOptions | null>(null);
  const [title, setTitle] = useState(source?.title ?? "");
  const [description, setDescription] = useState(source?.description ?? "");
  const [mandatory, setMandatory] = useState("");
  const [outcome, setOutcome] = useState("");
  const [criteria, setCriteria] = useState([{ label: "", required: true }]);
  const [locationType, setLocationType] = useState(source?.roomId ? "room" : source?.areaId ? "area" : "department");
  const [location, setLocation] = useState(String(source?.roomId ?? source?.areaId ?? ""));
  const [plan, setPlan] = useState<PlanDraft>({
    priority: source?.priorityId ? String(source.priorityId) : "",
    start: "",
    due: localPlanTime(source?.dueAt ?? null),
    minutes: "",
  });
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [hidden, setHidden] = useState(false);
  const [revision, setRevision] = useState(0);
  const [repeat, setRepeat] = useState(false);
  const [recurrence, setRecurrence] = useState<RecurrenceInput>({
    rule: "DAILY",
    weekdays: [],
    monthDay: null,
    defaultAssigneeId: 0,
    endDate: null,
  });
  const [pending, setPending] = useState<{
    input: CreateTaskInput;
    assign: boolean;
    recurrence: RecurrenceInput | null;
  } | null>(null);
  const key = useRef(crypto.randomUUID());
  const saving = useRef(false);
  const active = useRef<AbortController | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    setOptions(null);
    setError("");
    setHidden(false);
    void creationOptions(
      branch,
      department ? Number(department) : null,
      controller.signal,
    )
      .then((data) => {
        if (!controller.signal.aborted) setOptions(data);
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        if (error instanceof TaskProgressError && error.status === 401)
          actions.invalidateSession(owner);
        if (error instanceof TaskProgressError && error.status === 403)
          setHidden(true);
        else
          setError(
            "Task creation options are unavailable. Reload to try again.",
          );
      });
    return () => controller.abort();
  }, [branch, department, owner, actions, revision]);
  useEffect(() => () => active.current?.abort(), []);
  async function save(assign: boolean) {
    if (saving.current || busy || (!options && !pending)) return;
    let submission: {
      input: CreateTaskInput;
      assign: boolean;
      recurrence: RecurrenceInput | null;
    };
    try {
      if (source && !pending && !["required", "optional"].includes(mandatory)) {
        setError("Choose whether this task is required for request completion."); return;
      }
      submission = pending ?? {
        assign,
        recurrence: repeat ? recurrence : null,
        input: {
          requestKey: key.current,
          departmentId: Number(department),
          categoryId: Number(category),
          title,
          description: description.trim() || null,
          requestedResult: outcome,
          criteria,
          priorityId: Number(plan.priority),
          plannedStartAt: utcPlanTime(plan.start),
          dueAt: utcPlanTime(plan.due),
          slaMinutes: plan.minutes ? Number(plan.minutes) : null,
          roomId: locationType === "room" ? Number(location) : null,
          areaId: locationType === "area" ? Number(location) : null,
          internalRequestId: source?.requestId ?? null,
          ...(source ? { requestLink: { version: source.version, mandatory: mandatory === "required" } } : {}),
        },
      };
      if (
        !pending &&
        ((plan.start && plan.due && plan.due < plan.start) ||
          !department ||
          !category ||
          !plan.priority ||
          !title.trim() ||
          !outcome.trim() ||
          criteria.some((c) => !c.label.trim()))
      ) {
        setError(
          "Complete the required fields; deadline must not precede planned start.",
        );
        return;
      }
      validateTaskCreation(submission.input);
      if (submission.recurrence)
        validateRecurrence(submission.input, submission.recurrence);
    } catch {
      setError(
        "Check task fields, unique completion criteria and planning dates. Recurring work also requires an eligible executor and a first deadline matching its schedule.",
      );
      return;
    }
    saving.current = true;
    setBusy(true);
    setError("");
    setPending(submission);
    const controller = new AbortController();
    active.current = controller;
    try {
      if (submission.recurrence) {
        await createSeries(
          branch,
          submission.input,
          submission.recurrence,
          controller.signal,
        );
        if (!controller.signal.aborted) onSeriesCreated();
        return;
      }
      const result = await createTask(
        branch,
        submission.input,
        controller.signal,
      );
      if (!controller.signal.aborted)
        onCreated(result.taskId, submission.assign);
    } catch (error) {
      if (controller.signal.aborted) return;
      if (error instanceof TaskProgressError && error.status === 401)
        actions.invalidateSession(owner);
      if (source && error instanceof TaskProgressError && [403, 409, 412].includes(error.status)) {
        setPending(null); setOptions(null); setHidden(true); onSourceInvalid?.(); return;
      }
      if (
        error instanceof TaskProgressError &&
        [400, 403, 422].includes(error.status)
      ) {
        setPending(null);
        setError(
          "Task was not created. Check your scope, category, location, criteria and planning fields.",
        );
      } else
        setError(
          "Creation could not be confirmed. Check the outcome using the same request before creating another task.",
        );
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  if (hidden) return null;
  return (
    <section aria-label="Create operational task">
      <h2>Create operational task</h2>
      {error && <p role="alert">{error}</p>}
      {pending && (
        <>
          <p>
            Creation reference: {pending.recurrence ? "SERIES-" : "TASK-"}
            {pending.input.requestKey.replaceAll("-", "").toUpperCase()}
          </p>
          <button disabled={busy} onClick={() => void save(pending.assign)}>
            Check creation outcome
          </button>
        </>
      )}
      {!options && !pending && (
        <button onClick={() => setRevision((n) => n + 1)}>
          Reload creation options
        </button>
      )}
      {options && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void save(false);
          }}
        >
          <fieldset disabled={busy || pending !== null}>
            <legend>Task definition</legend>
            {source && <><p>Linked to request {source.requestId}. The request stays open after creation.</p>
              <label>Request completion requirement<select aria-label="Request completion requirement" required value={mandatory} onChange={e => setMandatory(e.target.value)}>
                <option value="">Select requirement</option><option value="required">Required for completion</option><option value="optional">Optional</option>
              </select></label></>}
            <label>
              Task title
              <input
                required
                maxLength={200}
                value={title}
                onChange={(e) => setTitle(e.target.value)}
              />
            </label>
            <label>
              Department
              <select
                aria-label="Department"
                required
                value={department}
                onChange={(e) => {
                  setDepartment(e.target.value);
                  setCategory("");
                  setRecurrence((r) => ({ ...r, defaultAssigneeId: 0 }));
                }}
              >
                <option value="">Choose department</option>
                {options.departments.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Work category
              <select
                aria-label="Work category"
                required
                value={category}
                onChange={(e) => setCategory(e.target.value)}
              >
                <option value="">Choose category</option>
                {options.categories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </label>
            <p>
              Cleaning, incident and material requests use their dedicated
              workflows.
            </p>
            <label>
              Location type
              <select
                aria-label="Location type"
                value={locationType}
                onChange={(e) => {
                  setLocationType(e.target.value);
                  setLocation("");
                }}
              >
                <option value="department">Department only</option>
                <option value="room">Room</option>
                <option value="area">Area</option>
              </select>
            </label>
            {locationType !== "department" && (
              <label>
                Location ID
                <input
                  required
                  inputMode="numeric"
                  pattern="[1-9][0-9]*"
                  value={location}
                  onChange={(e) => setLocation(e.target.value)}
                />
              </label>
            )}
            <label>
              Task description
              <textarea
                aria-label="Task description"
                maxLength={4000}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              />
            </label>
            <label>
              Expected outcome
              <textarea
                aria-label="Expected outcome"
                required
                maxLength={4000}
                value={outcome}
                onChange={(e) => setOutcome(e.target.value)}
              />
            </label>
            <fieldset>
              <legend>Completion criteria</legend>
              {criteria.map((criterion, index) => (
                <div key={index}>
                  <label>
                    Criterion {index + 1}
                    <input
                      required
                      maxLength={250}
                      value={criterion.label}
                      onChange={(e) =>
                        setCriteria((rows) =>
                          rows.map((c, i) =>
                            i === index ? { ...c, label: e.target.value } : c,
                          ),
                        )
                      }
                    />
                  </label>
                  <label>
                    <input
                      type="checkbox"
                      checked={criterion.required}
                      onChange={(e) =>
                        setCriteria((rows) =>
                          rows.map((c, i) =>
                            i === index
                              ? { ...c, required: e.target.checked }
                              : c,
                          ),
                        )
                      }
                    />
                    Mandatory criterion {index + 1}
                  </label>
                  <button
                    type="button"
                    disabled={criteria.length === 1}
                    onClick={() =>
                      setCriteria((rows) => rows.filter((_, i) => i !== index))
                    }
                  >
                    Remove criterion {index + 1}
                  </button>
                </div>
              ))}
              <button
                type="button"
                disabled={criteria.length >= 100}
                onClick={() =>
                  setCriteria((rows) => [
                    ...rows,
                    { label: "", required: true },
                  ])
                }
              >
                Add criterion
              </button>
            </fieldset>
            <TaskPlanFields
              value={plan}
              onChange={setPlan}
              priorities={options.priorities}
            />
            {!source && options.recurringAvailable && (
              <label>
                <input
                  type="checkbox"
                  checked={repeat}
                  onChange={(e) => setRepeat(e.target.checked)}
                />
                Repeat task
              </label>
            )}
            {repeat && (
              <TaskRecurrenceFields
                value={recurrence}
                onChange={setRecurrence}
                executors={options.defaultExecutors ?? []}
              />
            )}
            <button>{repeat ? "Save recurring series" : "Save task"}</button>
            <button
              type="button"
              disabled={repeat}
              onClick={(e) => {
                if (e.currentTarget.form?.reportValidity()) void save(true);
              }}
            >
              Save and assign
            </button>
          </fieldset>
        </form>
      )}
    </section>
  );
}
