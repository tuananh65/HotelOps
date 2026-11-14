import { useEffect, useRef, useState, type FormEvent } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { uploadFile } from "../api/dev1/cleaningProgress";
import {
  readTaskProgress,
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
  const [percent, setPercent] = useState(0);
  const [items, setItems] = useState<Criterion[]>([]);
  const [note, setNote] = useState("");
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
    if (!current || busy || locked || saving.current) return;
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
  return (
    <section aria-label="Task progress editor">
      <h3>Update progress</h3>
      <button disabled={busy} onClick={() => setReload((n) => n + 1)}>
        Reload progress (discard edits)
      </button>
      {busy && <p role="status">Loading or saving progress…</p>}
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
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
          <fieldset disabled={busy || locked}>
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
