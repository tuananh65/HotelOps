import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressError } from "./progress";
import {
  readSeries,
  stopSeries,
  type SeriesPage,
  type TaskSeries,
} from "./series";

export function TaskSeriesPanel({
  branch,
  owner,
  seriesId,
  taskId,
}: {
  branch: number;
  owner: AuthorityGeneration;
  seriesId?: number;
  taskId?: number;
}) {
  const { actions } = useAuth();
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [data, setData] = useState<SeriesPage | null>(null);
  const [selected, setSelected] = useState<TaskSeries | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [hidden, setHidden] = useState(false);
  const saving = useRef(false);
  const active = useRef<AbortController | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    setData(null);
    setSelected(null);
    setConfirmed(false);
    setReason("");
    setError("");
    setHidden(false);
    setUncertain(false);
    void readSeries(branch, page, controller.signal, seriesId)
      .then((result) => {
        if (!controller.signal.aborted) setData(result);
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        if (error instanceof TaskProgressError && error.status === 401)
          actions.invalidateSession(owner);
        if (
          error instanceof TaskProgressError &&
          [403, 503].includes(error.status)
        )
          setHidden(true);
        else
          setError("Recurring tasks could not be loaded. Reload to try again.");
      });
    return () => controller.abort();
  }, [branch, owner, actions, page, revision, seriesId]);
  useEffect(() => () => active.current?.abort(), []);
  async function stop() {
    if (!selected || !confirmed || saving.current || busy || uncertain) return;
    saving.current = true;
    setBusy(true);
    setError("");
    setMessage("");
    const controller = new AbortController();
    active.current = controller;
    try {
      await stopSeries(
        branch,
        selected,
        reason.trim() || null,
        controller.signal,
        taskId,
      );
      if (!controller.signal.aborted) {
        setMessage(
          "MSG08: Series stopped. Already generated tasks remain unchanged.",
        );
        setRevision((n) => n + 1);
      }
    } catch (error) {
      if (controller.signal.aborted) return;
      if (error instanceof TaskProgressError && error.status === 401)
        actions.invalidateSession(owner);
      setUncertain(true);
      setError(
        "Stop could not be confirmed or the series changed. Reload the series before any further action.",
      );
    } finally {
      saving.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  if (hidden) return null;
  return (
    <section aria-label="Recurring task series">
      <h2>Recurring task series</h2>
      <button disabled={busy} onClick={() => setRevision((n) => n + 1)}>
        Reload series
      </button>
      {message && <p role="status">{message}</p>}
      {error && <p role="alert">{error}</p>}
      {data && (
        <>
          <p>{data.totalCount} series</p>
          <ul>
            {data.items.map((series) => (
              <li key={series.seriesId}>
                <strong>{series.title}</strong> —{" "}
                {series.isActive ? "Active" : "Inactive"}; {series.rule};
                executor: {series.defaultAssigneeName}; generated tasks:{" "}
                {series.occurrenceCount}.
                <p>
                  First deadline: {series.firstDueAt}; next deadline:{" "}
                  {series.nextDueAt ?? "Schedule exhausted"}; end date:{" "}
                  {series.endDate ?? "No end date"}.
                </p>
                {series.isActive && (
                  <button
                    disabled={busy || uncertain}
                    onClick={() => {
                      setSelected(series);
                      setConfirmed(false);
                      setReason("");
                    }}
                  >
                    Stop series: {series.title}
                  </button>
                )}
              </li>
            ))}
          </ul>
          <nav aria-label="Series pagination">
            <button
              disabled={busy || page === 1}
              onClick={() => setPage((n) => n - 1)}
            >
              Previous series
            </button>
            <span>Series page {page}</span>
            <button
              disabled={busy || page * data.pageSize >= data.totalCount}
              onClick={() => setPage((n) => n + 1)}
            >
              Next series
            </button>
          </nav>
        </>
      )}
      {selected && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void stop();
          }}
        >
          <fieldset disabled={busy || uncertain}>
            <legend>Stop {selected.title}</legend>
            <p>
              This stops future occurrences only. Existing tasks, assignments,
              deadlines and work intervals remain unchanged.
            </p>
            <label>
              Stop series reason
              <textarea
                maxLength={4000}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
            <label>
              <input
                type="checkbox"
                required
                checked={confirmed}
                onChange={(e) => setConfirmed(e.target.checked)}
              />
              Confirm stop future occurrences
            </label>
            <button disabled={!confirmed}>Confirm stop series</button>
            <button type="button" onClick={() => setSelected(null)}>
              Keep series active
            </button>
          </fieldset>
        </form>
      )}
    </section>
  );
}
