import { useEffect, useRef, useState, type FormEvent } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { initializeTaskProgress, TaskProgressError } from "./progress";

export function TaskCriteriaSetup({
  branch,
  task,
  owner,
  onInitialized,
}: {
  branch: number;
  task: number;
  owner: AuthorityGeneration;
  onInitialized: () => void;
}) {
  const { actions } = useAuth();
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState([{ label: "", required: true }]);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [locked, setLocked] = useState(false);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState(false);
  const active = useRef<AbortController | null>(null);
  const inFlight = useRef(false);
  useEffect(() => () => active.current?.abort(), []);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (inFlight.current || locked || saved) return;
    inFlight.current = true;
    setBusy(true);
    setError("");
    const controller = new AbortController();
    active.current = controller;
    try {
      await initializeTaskProgress(
        branch,
        task,
        items,
        reason,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setSaved(true);
        onInitialized();
      }
    } catch (e) {
      if (controller.signal.aborted) return;
      if (e instanceof TaskProgressError && e.status === 401)
        actions.invalidateSession(owner);
      setLocked(true);
      setError(
        e instanceof TaskProgressError && e.status === 403
          ? "You do not have permission to define criteria for this task."
          : e instanceof TaskProgressError && e.status === 409
            ? "Criteria already exist or the task has not started. Existing criteria were not replaced."
            : "Setup could not be confirmed. Refresh the task before trying again; the request was not retried.",
      );
    } finally {
      inFlight.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  return (
    <section aria-label="Task criteria setup">
      {!open ? (
        <button onClick={() => setOpen(true)}>
          Set up completion criteria
        </button>
      ) : (
        <>
          <h3>Set up completion criteria</h3>
          <p>
            For an existing started task without criteria. Definitions are
            permanent after setup. Task management permission is checked when
            saving.
          </p>
          {error && <p role="alert">{error}</p>}
          {saved ? (
            <p role="status">Completion criteria initialized.</p>
          ) : (
            <form onSubmit={submit}>
              <fieldset disabled={busy || locked}>
                <legend>Criterion definitions</legend>
                {items.map((item, index) => (
                  <div key={index}>
                    <label>
                      Criterion {index + 1}
                      <input
                        required
                        maxLength={250}
                        value={item.label}
                        onChange={(e) =>
                          setItems((rows) =>
                            rows.map((r, n) =>
                              n === index ? { ...r, label: e.target.value } : r,
                            ),
                          )
                        }
                      />
                    </label>
                    <label>
                      <input
                        type="checkbox"
                        checked={item.required}
                        onChange={(e) =>
                          setItems((rows) =>
                            rows.map((r, n) =>
                              n === index
                                ? { ...r, required: e.target.checked }
                                : r,
                            ),
                          )
                        }
                      />
                      Required criterion {index + 1}
                    </label>
                    <button
                      type="button"
                      disabled={items.length === 1}
                      onClick={() =>
                        setItems((rows) => rows.filter((_, n) => n !== index))
                      }
                    >
                      Remove criterion {index + 1}
                    </button>
                  </div>
                ))}
                <button
                  type="button"
                  disabled={items.length >= 100}
                  onClick={() =>
                    setItems((rows) => [...rows, { label: "", required: true }])
                  }
                >
                  Add criterion
                </button>
                <label>
                  Setup reason
                  <input
                    required
                    minLength={10}
                    maxLength={500}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  />
                </label>
                <button>Initialize criteria</button>
              </fieldset>
            </form>
          )}
        </>
      )}
    </section>
  );
}
