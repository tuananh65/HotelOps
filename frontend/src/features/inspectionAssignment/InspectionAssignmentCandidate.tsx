import { useEffect, useState } from "react";
import {
  InspectionAssignmentClient,
  assignmentError,
  type AssignmentQueue,
  type AssignmentOptions,
} from "./client";
const api = new InspectionAssignmentClient();
export function InspectionAssignmentCandidate({
  branchId,
  onOpenInspection,
}: {
  branchId: number;
  onOpenInspection: (id: number) => void;
}) {
  return (
    <Assignment
      key={branchId}
      branchId={branchId}
      onOpenInspection={onOpenInspection}
    />
  );
}
function Assignment({
  branchId,
  onOpenInspection,
}: {
  branchId: number;
  onOpenInspection: (id: number) => void;
}) {
  const [queue, setQueue] = useState<AssignmentQueue>({
      pending: [],
      assigned: [],
    }),
    [source, setSource] = useState<AssignmentOptions | null>(null),
    [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [inspector, setInspector] = useState(""),
    [supervisor, setSupervisor] = useState(""),
    [due, setDue] = useState(""),
    [reason, setReason] = useState(""),
    [review, setReview] = useState(false),
    [confirmed, setConfirmed] = useState(false),
    [blocked, setBlocked] = useState(false);
  useEffect(() => {
    const c = new AbortController();
    setBusy(true);
    api
      .queue(branchId, c.signal)
      .then(setQueue)
      .catch((e) => {
        if (!c.signal.aborted) setError(assignmentError(e));
      })
      .finally(() => {
        if (!c.signal.aborted) setBusy(false);
      });
    return () => c.abort();
  }, [branchId]);
  async function open(task: number) {
    setBusy(true);
    setError("");
    setReview(false);
    setConfirmed(false);
    try {
      setSource(await api.options(branchId, task));
      setInspector("");
      setSupervisor("");
      setBlocked(false);
    } catch (e) {
      setError(assignmentError(e));
      setBlocked(true);
    } finally {
      setBusy(false);
    }
  }
  async function confirm() {
    if (!source || !confirmed || blocked || busy) return;
    setBusy(true);
    try {
      await api.confirm(
        branchId,
        source,
        Number(inspector),
        Number(supervisor),
        new Date(due).toISOString(),
        reason,
        true,
      );
    } catch (e) {
      setError(assignmentError(e));
      setBlocked(true);
    } finally {
      setBusy(false);
    }
  }
  const valid = source && inspector && supervisor && due && reason.trim();
  return (
    <section aria-label="Inspection assignment">
      <h2>Assign inspection</h2>
      {busy && <p role="status">Loading...</p>}
      {error && <p role="alert">{error}</p>}
      <h3>Pending inspections</h3>
      {!busy && !queue.pending.length && <p>No pending tasks.</p>}
      {queue.pending.map((t) => (
        <button disabled={busy} key={t.taskId} onClick={() => open(t.taskId)}>
          Room #{t.roomId} · Task #{t.taskId}
        </button>
      ))}
      <h3>Assigned to me</h3>
      {queue.assigned.map((i) => (
        <button
          key={i.inspectionId}
          onClick={() => onOpenInspection(i.inspectionId)}
        >
          Open Inspection #{i.inspectionId}
        </button>
      ))}
      {source && (
        <div>
          <p>
            Room #{source.roomId}; checklist version {source.checklistVersionId}
          </p>
          <fieldset disabled={busy || blocked || review}>
            <label>
              Inspector
              <select
                value={inspector}
                onChange={(e) => setInspector(e.target.value)}
              >
                <option value="">Choose inspector</option>
                {source.inspectors.map((p) => (
                  <option key={p.userId} value={p.userId}>
                    {p.fullName}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Responsible supervisor
              <select
                value={supervisor}
                onChange={(e) => setSupervisor(e.target.value)}
              >
                <option value="">Explicitly choose supervisor</option>
                {source.responsibleSupervisors.map((p) => (
                  <option key={p.userId} value={p.userId}>
                    {p.fullName}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Due time
              <input
                type="datetime-local"
                value={due}
                onChange={(e) => setDue(e.target.value)}
              />
            </label>
            <label>
              Reason
              <input
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
          </fieldset>
          <button
            disabled={!valid || busy || blocked}
            onClick={() => {
              setReview(true);
              setConfirmed(false);
            }}
          >
            Review assignment
          </button>
          {review && (
            <div>
              <p>
                Confirm the selected inspector, explicit responsible supervisor,
                checklist and due time.
              </p>
              <label>
                <input
                  type="checkbox"
                  checked={confirmed}
                  disabled={busy || blocked}
                  onChange={(e) => setConfirmed(e.target.checked)}
                />
                I confirm this assignment
              </label>
              <button
                disabled={!confirmed || busy || blocked}
                onClick={confirm}
              >
                Confirm assignment
              </button>
            </div>
          )}
          <p>
            Production assignment is currently unavailable; required
            notification has not been accepted.
          </p>
          <button disabled={busy} onClick={() => open(source.taskId)}>
            Reload assignment source
          </button>
        </div>
      )}
    </section>
  );
}
