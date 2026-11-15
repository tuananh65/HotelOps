import { useEffect, useRef, useState } from "react";
import {
  createInspectionApi,
  InspectionApiError,
  type ReworkOptions,
} from "./api";

export function RequestReworkForm({
  branch,
  id,
  onDenied,
  onSessionInvalid,
}: {
  branch: number;
  id: number;
  onDenied: (status: 401 | 403) => void;
  onSessionInvalid?: () => void;
}) {
  const [options, setOptions] = useState<ReworkOptions | null>(null);
  const [selected, setSelected] = useState<number[]>([]);
  const [assignee, setAssignee] = useState("");
  const [due, setDue] = useState("");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [receipt, setReceipt] = useState<{
    id: number;
    notification: string;
  } | null>(null);
  const [reload, setReload] = useState(0);
  const lifetime = useRef<AbortController | null>(null);
  const writing = useRef(false);
  function failed(e: unknown) {
    if (
      e instanceof InspectionApiError &&
      (e.status === 401 || e.status === 403)
    )
      onDenied(e.status);
    setError(
      e instanceof InspectionApiError && e.uncertain
        ? "Write outcome unknown. Reload options to verify; do not automatically retry."
        : e instanceof Error
          ? e.message
          : "Rework unavailable",
    );
    setOptions(null);
  }
  useEffect(() => {
    const scope = new AbortController();
    lifetime.current = scope;
    setOptions(null);
    setReceipt(null);
    setError("");
    setAssignee("");
    setBusy(true);
    const api = createInspectionApi({ signal: scope.signal, onSessionInvalid });
    void api
      .reworkOptions(branch, id)
      .then((value) => {
        if (scope.signal.aborted) return;
        setOptions(value);
        setSelected(value.defects.map((d) => d.id));
      })
      .catch((e) => {
        if (!scope.signal.aborted) failed(e);
      })
      .finally(() => {
        if (!scope.signal.aborted) setBusy(false);
      });
    return () => {
      scope.abort();
      if (lifetime.current === scope) lifetime.current = null;
    };
    // Identity is keyed by the parent. Callback changes cannot replay requests.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branch, id, reload]);
  async function save() {
    const scope = lifetime.current;
    if (
      !scope ||
      scope.signal.aborted ||
      busy ||
      writing.current ||
      !options ||
      receipt
    )
      return;
    writing.current = true;
    setBusy(true);
    setError("");
    try {
      const api = createInspectionApi({
        signal: scope.signal,
        onSessionInvalid,
      });
      const result = await api.requestRework(
        branch,
        id,
        options,
        Number(assignee),
        selected,
        due,
        reason,
      );
      if (!scope.signal.aborted) setReceipt(result);
    } catch (e) {
      if (!scope.signal.aborted) failed(e);
    } finally {
      writing.current = false;
      if (!scope.signal.aborted) setBusy(false);
    }
  }
  return (
    <section aria-label="Request cleaning rework">
      <h3>Request cleaning rework</h3>
      {error && <p role="alert">{error}</p>}
      {busy && <p role="status">Loading or saving rework...</p>}
      <button
        type="button"
        disabled={busy}
        onClick={() => setReload((v) => v + 1)}
      >
        Reload rework options
      </button>
      {receipt ? (
        <p role="status">
          Rework #{receipt.id} is pending acceptance. Notification:{" "}
          {receipt.notification}.
        </p>
      ) : options?.existingReworkId ? (
        <p role="alert">
          MSG03: Open rework #{options.existingReworkId} already exists.
        </p>
      ) : (
        options && (
          <fieldset disabled={busy}>
            <legend>Defects, assignee and due time</legend>
            {options.defects.map((d) => (
              <label key={d.id}>
                <input
                  type="checkbox"
                  checked={selected.includes(d.id)}
                  onChange={(e) =>
                    setSelected((ids) =>
                      e.target.checked
                        ? [...ids, d.id]
                        : ids.filter((v) => v !== d.id),
                    )
                  }
                />
                {d.description}
              </label>
            ))}
            <label>
              Housekeeper
              <select
                value={assignee}
                onChange={(e) => setAssignee(e.target.value)}
              >
                <option value="">Select eligible housekeeper</option>
                {options.assignees.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name}
                    {a.originalCleaner ? " (original cleaner)" : ""}
                  </option>
                ))}
              </select>
            </label>
            {!options.assignees.length && (
              <p role="status">
                No eligible housekeeper is currently available.
              </p>
            )}
            <label>
              Rework due time
              <input
                type="datetime-local"
                value={due}
                onChange={(e) => setDue(e.target.value)}
              />
            </label>
            <label>
              Rework reason
              <input
                maxLength={500}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
            <button
              type="button"
              disabled={!assignee || !selected.length || !due || !reason.trim()}
              onClick={() => void save()}
            >
              Save rework request
            </button>
          </fieldset>
        )
      )}
    </section>
  );
}
