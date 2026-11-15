import { useEffect, useRef, useState, type FormEvent } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { uploadFile } from "../api/dev1/cleaningProgress";
import { submitTask } from "./submission";
import {
  readTaskProgress,
  enterTask,
  resumeTask,
  pauseTask,
  readPauseReasons,
  type PauseReason,
  saveTaskProgress,
  TaskProgressError,
  type TaskProgressSnapshot,
  type Criterion,
} from "./progress";

// Parent remounts this editor for each admitted authority and task identity.
export function TaskProgressPanel({
  branch,
  task,
  owner,
}: {
  branch: number;
  task: number;
  owner: AuthorityGeneration;
}) {
  const { actions } = useAuth();
  const [current, setCurrent] = useState<TaskProgressSnapshot | null>(null);
  const [reasons, setReasons] = useState<PauseReason[]>([]);
  const [reasonId, setReasonId] = useState(0);
  const [pauseNote, setPauseNote] = useState("");
  const [percent, setPercent] = useState(0);
  const [items, setItems] = useState<Criterion[]>([]);
  const [note, setNote] = useState("");
  const [completing, setCompleting] = useState(false);
  const [resultNote, setResultNote] = useState("");
  const [handoverNote, setHandoverNote] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [busy, setBusy] = useState(true);
  const [locked, setLocked] = useState(true);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [reload, setReload] = useState(0);
  const active = useRef<AbortController | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const saving = useRef(false);
  function show(value: TaskProgressSnapshot) {
    setCurrent(value);
    setPercent(value.data.percent);
    setItems(value.data.items);
    setNote("");
    setCompleting(false);
    setResultNote("");
    setHandoverNote("");
    setFiles([]);
    if (fileInput.current) fileInput.current.value = "";
  }
  function failure(reason: unknown) {
    if (reason instanceof TaskProgressError && reason.status === 401)
      actions.invalidateSession(owner);
    setError(
      reason instanceof TaskProgressError && reason.status === 403
        ? "Only the currently assigned executor with access can update this task."
        : reason instanceof TaskProgressError && reason.status === 412
          ? "This task changed. Reload and review it before saving again."
          : "Progress could not be confirmed. Reload before saving again.",
    );
    setLocked(true);
  }
  useEffect(() => {
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setLocked(true);
    setCurrent(null);
    setError("");
    setMessage("");
    void readTaskProgress(branch, task, controller.signal)
      .then((value) => {
        if (!controller.signal.aborted) {
          show(value);
          setLocked(false);
        }
      })
      .catch((reason) => {
        if (!controller.signal.aborted) failure(reason);
      })
      .finally(() => {
        if (!controller.signal.aborted) setBusy(false);
      });
    return () => {
      controller.abort();
      active.current?.abort();
    };
  }, [branch, task, owner, reload, actions]);
  async function save(event: FormEvent) {
    event.preventDefault();
    if (
      !current ||
      !["IN_PROGRESS", "PAUSED"].includes(current.data.state) ||
      busy ||
      locked ||
      saving.current
    )
      return;
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      if (files.length > 5) throw new Error("Too many files");
      const photos = await Promise.all(files.map(uploadFile));
      controller.signal.throwIfAborted();
      const updated = await saveTaskProgress(
        branch,
        task,
        current,
        {
          percent,
          note: note.trim() || null,
          items: items
            .filter((i) => i.outcome !== null)
            .map((i) => ({
              id: i.id,
              outcome: i.outcome!,
              note: i.note?.trim() || null,
            })),
          photos,
        },
        controller.signal,
      );
      if (!controller.signal.aborted) {
        show(updated);
        setMessage(
          "Progress saved. Reaching 100% does not complete this task.",
        );
      }
    } catch (reason) {
      if (!controller.signal.aborted) failure(reason);
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  async function pause(loadOnly = false) {
    if (!current || busy || locked || saving.current) return;
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      if (loadOnly) {
        const values = await readPauseReasons(branch, task, controller.signal);
        if (!controller.signal.aborted) setReasons(values);
      } else {
        const selected = reasons.find((r) => r.id === reasonId);
        if (!selected || (selected.code === "OTHER" && !pauseNote.trim())) {
          setError("Select a pause reason; Other requires a note.");
          return;
        }
        const value = await pauseTask(
          branch,
          task,
          current,
          controller.signal,
          reasonId,
          pauseNote,
        );
        if (!controller.signal.aborted) {
          setCurrent(value);
          setReasonId(0);
          setPauseNote("");
          setMessage("Task paused. Working interval closed.");
        }
      }
    } catch (reason) {
      if (!controller.signal.aborted) failure(reason);
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  async function resume() {
    if (!current || busy || locked || saving.current) return;
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const updated = await resumeTask(
        branch,
        task,
        current,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setCurrent(updated);
        setMessage("Task resumed. Working time is now being recorded.");
      }
    } catch (reason) {
      if (!controller.signal.aborted) {
        failure(reason);
        if (reason instanceof TaskProgressError && reason.status === 409)
          setError(
            "Task could not be resumed. A working workday and no other open interval are required. Reload to review its current state.",
          );
      }
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  async function enter(action: "accept" | "start") {
    if (!current || busy || locked || saving.current) return;
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const updated = await enterTask(
        branch,
        task,
        current,
        action,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        show(updated);
        setMessage(
          action === "accept"
            ? "Task accepted."
            : "Task started. Working time is now being recorded.",
        );
      }
    } catch (reason) {
      if (!controller.signal.aborted) {
        failure(reason);
        if (
          reason instanceof TaskProgressError &&
          reason.messageCode === "MSG30"
        )
          setError(
            "MSG30: Start a working workday before starting this task. Reload to try again.",
          );
        else if (
          reason instanceof TaskProgressError &&
          reason.messageCode === "MSG31"
        )
          setError(
            "MSG31: Pause, hold or complete your current work item before starting this task. Reload to try again.",
          );
      }
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  async function complete() {
    if (
      !current ||
      current.data.state !== "IN_PROGRESS" ||
      busy ||
      locked ||
      saving.current
    )
      return;
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      if (files.length > 5) throw new Error("Too many evidence files");
      const photos = await Promise.all(files.map(uploadFile));
      controller.signal.throwIfAborted();
      await submitTask(
        branch,
        task,
        current,
        {
          items: items
            .filter((i) => i.outcome !== null)
            .map((i) => ({
              id: i.id,
              outcome: i.outcome!,
              note: i.note?.trim() || null,
            })),
          resultNote: resultNote.trim() || null,
          handoverNote: handoverNote.trim() || null,
          photos,
        },
        controller.signal,
      );
      const updated = await readTaskProgress(branch, task, controller.signal);
      if (!controller.signal.aborted) {
        show(updated);
        setMessage(
          "MSG09: Task submitted for verification. Working interval closed.",
        );
      }
    } catch (reason) {
      if (!controller.signal.aborted) {
        if (
          reason instanceof TaskProgressError &&
          reason.messageCode === "MSG03"
        )
          setError(
            `MSG03: Record results for required criteria: ${reason.missingCriteria.join(", ")}.`,
          );
        else if (
          reason instanceof TaskProgressError &&
          reason.messageCode === "MSG23"
        )
          setError(
            "MSG23: Evidence must be PNG, JPEG or PDF, at most 10 MB per file.",
          );
        else failure(reason);
      }
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  return (
    <section aria-label="Task progress editor">
      <h3>Update progress</h3>
      <button disabled={busy} onClick={() => setReload((n) => n + 1)}>
        Reload progress (discard edits)
      </button>
      {busy && <p role="status">Loading or saving progress…</p>}
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
      {current?.data.state === "PENDING_VERIFICATION" && (
        <p>Awaiting Department Head verification. Progress is locked.</p>
      )}
      {current?.data.state === "IN_PROGRESS" && (
        <section aria-label="Task completion">
          <button disabled={busy || locked} onClick={() => setCompleting(true)}>
            Complete task
          </button>
          {completing && (
            <>
              <p>
                Review criterion results and evidence below before submitting.
              </p>
              <label>
                Result note
                <textarea
                  maxLength={4000}
                  disabled={busy || locked}
                  value={resultNote}
                  onChange={(e) => setResultNote(e.target.value)}
                />
              </label>
              <label>
                Handover note
                <textarea
                  maxLength={4000}
                  disabled={busy || locked}
                  value={handoverNote}
                  onChange={(e) => setHandoverNote(e.target.value)}
                />
              </label>
              <button disabled={busy || locked} onClick={() => void complete()}>
                Submit completion
              </button>
            </>
          )}
        </section>
      )}
      {current?.data.state === "ASSIGNED" && (
        <button disabled={busy || locked} onClick={() => void enter("accept")}>
          Accept task
        </button>
      )}
      {current?.data.state === "ACCEPTED" && (
        <button disabled={busy || locked} onClick={() => void enter("start")}>
          Start task
        </button>
      )}
      {current?.data.state === "IN_PROGRESS" && (
        <section aria-label="Pause task controls">
          <button disabled={busy || locked} onClick={() => void pause(true)}>
            Load pause reasons
          </button>
          <label>
            Pause reason
            <select
              aria-label="Pause reason"
              disabled={busy || locked}
              value={reasonId}
              onChange={(e) => setReasonId(Number(e.target.value))}
            >
              <option value={0}>Select reason</option>
              {reasons.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Pause note
            <textarea
              maxLength={1000}
              disabled={busy || locked}
              value={pauseNote}
              onChange={(e) => setPauseNote(e.target.value)}
            />
          </label>
          <p>Other requires a note. Maximum 1000 characters.</p>
          <button
            disabled={busy || locked || !reasonId}
            onClick={() => void pause()}
          >
            Pause task
          </button>
        </section>
      )}
      {current?.data.state === "PAUSED" && (
        <button disabled={busy || locked} onClick={() => void resume()}>
          Resume task
        </button>
      )}
      {current && (
        <section aria-label="Saved task evidence">
          <h4>Saved evidence</h4>
          {current.data.evidence.length === 0 ? (
            <p>No evidence uploaded.</p>
          ) : (
            <ul>
              {current.data.evidence.map((file) => (
                <li key={file.id}>
                  <a href={file.downloadPath} download={file.name}>
                    {file.name}
                  </a>
                  {file.bytes !== null && <> — {file.bytes} bytes</>}
                  <time dateTime={file.uploadedAt}> — {file.uploadedAt}</time>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
      {current && (
        <form onSubmit={save}>
          <p>Task execution state: {current.data.state}</p>
          <fieldset
            disabled={
              busy ||
              locked ||
              !["IN_PROGRESS", "PAUSED"].includes(current.data.state)
            }
          >
            <legend>Progress and completion criteria</legend>
            <label>
              Progress (%)
              <input
                type="number"
                required
                min={0}
                max={100}
                step={1}
                value={Number.isNaN(percent) ? "" : percent}
                onChange={(e) =>
                  setPercent(
                    e.target.value === "" ? NaN : Number(e.target.value),
                  )
                }
              />
            </label>
            {items.length === 0 && (
              <p>No completion criteria are configured.</p>
            )}
            {items.map((item, index) => (
              <div key={item.id}>
                <label>
                  {item.label}
                  {item.required ? " (required for completion)" : ""}
                  <select
                    aria-label={`Criterion result: ${item.label}`}
                    value={item.outcome ?? ""}
                    onChange={(e) =>
                      setItems((rows) =>
                        rows.map((r, n) =>
                          n === index
                            ? {
                                ...r,
                                outcome: e.target.value as "PASS" | "FAIL",
                              }
                            : r,
                        ),
                      )
                    }
                  >
                    <option value="" disabled>
                      Not recorded
                    </option>
                    <option value="PASS">Pass</option>
                    <option value="FAIL">Fail</option>
                  </select>
                </label>
                <label>
                  Criterion note: {item.label}
                  <input
                    maxLength={1000}
                    disabled={item.outcome === null}
                    value={item.note ?? ""}
                    onChange={(e) =>
                      setItems((rows) =>
                        rows.map((r, n) =>
                          n === index ? { ...r, note: e.target.value } : r,
                        ),
                      )
                    }
                  />
                </label>
              </div>
            ))}
            <label>
              Progress note
              <input
                maxLength={4000}
                value={note}
                onChange={(e) => setNote(e.target.value)}
              />
            </label>
            <label>
              Evidence files (up to 5, PNG/JPEG/PDF, 10 MB each)
              <input
                ref={fileInput}
                type="file"
                multiple
                accept="image/png,image/jpeg,application/pdf"
                onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
              />
            </label>
            <button>Save progress</button>
          </fieldset>
        </form>
      )}
    </section>
  );
}
