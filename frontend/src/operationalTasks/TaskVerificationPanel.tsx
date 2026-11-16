import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressError } from "./progress";
import {
  readVerification,
  decideVerification,
  type VerificationSnapshot,
} from "./verification";

export function TaskVerificationPanel({
  branch,
  task,
  owner,
  onChanged,
}: {
  branch: number;
  task: number;
  owner: AuthorityGeneration;
  onChanged?: (message: string) => void;
}) {
  const { actions } = useAuth();
  const [snapshot, setSnapshot] = useState<VerificationSnapshot | null>(null);
  const [note, setNote] = useState("");
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [locked, setLocked] = useState(true);
  const [reload, setReload] = useState(0);
  const active = useRef<AbortController | null>(null);
  const saving = useRef(false);
  function failure(reason: unknown) {
    if (reason instanceof TaskProgressError && reason.status === 401)
      actions.invalidateSession(owner);
    setError(
      reason instanceof TaskProgressError && reason.messageCode === "MSG22"
        ? "MSG22: The requester, submitter or performer cannot verify this cycle."
        : reason instanceof TaskProgressError && reason.status === 412
          ? "The task changed. Reload and review before deciding again."
          : "Verification is unavailable or its outcome could not be confirmed. Reload before deciding again.",
    );
    setLocked(true);
  }
  useEffect(() => {
    const controller = new AbortController();
    active.current = controller;
    setSnapshot(null);
    setError("");
    setNote("");
    setLocked(true);
    setBusy(true);
    setMessage("");
    void readVerification(branch, task, controller.signal)
      .then((value) => {
        if (!controller.signal.aborted) {
          setSnapshot(value);
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
  async function decide(decision: "ACCEPT" | "RETURN") {
    if (!snapshot?.data.canDecide || locked || busy || saving.current) return;
    if (note.trim().length < (decision === "RETURN" ? 10 : 1)) {
      setError(
        decision === "RETURN"
          ? "Return reason must contain at least 10 characters."
          : "Enter a verification note.",
      );
      return;
    }
    saving.current = true;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const result = await decideVerification(
        branch,
        task,
        snapshot,
        decision,
        note,
        controller.signal,
      );
      if (!controller.signal.aborted && onChanged) {
        setLocked(true);
        onChanged(result.messageCode + (decision === "ACCEPT" ? ": Task closed." : ": Task returned to the same assignee. Start again to continue work."));
        return;
      }
      const updated = await readVerification(branch, task, controller.signal);
      if (!controller.signal.aborted) {
        setSnapshot(updated);
        setNote("");
        setMessage(
          result.messageCode +
            (decision === "ACCEPT"
              ? ": Task closed."
              : ": Task returned to the same assignee."),
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
    <section aria-label="Task verification">
      <h3>Task verification</h3>
      <button disabled={busy} onClick={() => setReload((n) => n + 1)}>
        Reload verification
      </button>
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
      {snapshot && (
        <>
          <p>Workflow: {snapshot.data.state}</p>
          <p>
            Actual effort: {snapshot.data.actualEffortSeconds} seconds (paused
            time excluded)
          </p>
          {snapshot.data.denialCode === "MSG22" && (
            <p>
              MSG22: The requester, submitter or performer cannot verify this
              cycle.
            </p>
          )}
          {snapshot.data.denialCode === "SOURCE_PROVENANCE_REQUIRED" && (
            <p>
              Verification is unavailable until the linked request can be
              validated.
            </p>
          )}
          {snapshot.data.canDecide && (
            <fieldset disabled={busy || locked}>
              <legend>Review submitted completion</legend>
              <label>
                Verification note or return reason
                <textarea
                  maxLength={4000}
                  value={note}
                  onChange={(event) => setNote(event.target.value)}
                />
              </label>
              <button onClick={() => void decide("ACCEPT")}>
                Accept completion
              </button>
              <button onClick={() => void decide("RETURN")}>
                Return for more work
              </button>
            </fieldset>
          )}
          <ol>
            {snapshot.data.decisions.map((d) => (
              <li key={d.submissionId}>
                {d.decision} by user {d.verifiedBy} at{" "}
                <time dateTime={d.decidedAt}>{d.decidedAt}</time>
                <p style={{ whiteSpace: "pre-wrap" }}>{d.note}</p>
              </li>
            ))}
          </ol>
        </>
      )}
    </section>
  );
}
