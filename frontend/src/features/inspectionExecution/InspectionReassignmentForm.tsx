import { useEffect, useRef, useState } from "react";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
import {
  readReassignment,
  reassignInspection,
  type ReassignmentOptions,
} from "../../api/dev1/inspectionReassignment";

export function InspectionReassignmentForm({
  branch,
  id,
}: {
  branch: string;
  id: number;
}) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    state.session &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [refresh, setRefresh] = useState(0);
  const key = `${branch}:${id}:${refresh}`;
  const current = useRef({ owner, key });
  current.current = { owner, key };
  const lifetime = useRef<AbortController | null>(null);
  const writing = useRef(false);
  const [loaded, setLoaded] = useState<{
    owner: typeof owner;
    key: string;
    data: ReassignmentOptions;
  } | null>(null);
  const [inspector, setInspector] = useState("");
  const [reason, setReason] = useState("");
  const [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [locked, setLocked] = useState(false);
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    lifetime.current = abort;
    setLoaded(null);
    setReview(false);
    setInspector("");
    setLocked(true);
    setMessage("");
    void readReassignment(branch, id, abort.signal)
      .then((data) => {
        if (!abort.signal.aborted) {
          setLoaded({ owner, key, data });
          setLocked(false);
        }
      })
      .catch((error) => {
        if (!abort.signal.aborted) {
          setMessage(
            "Changing inspector is unavailable for this inspection or your current access.",
          );
          if (error instanceof ApiError && error.status === 401)
            actions.invalidateSession(owner);
        }
      });
    return () => {
      abort.abort();
      if (lifetime.current === abort) lifetime.current = null;
    };
  }, [owner, key, branch, id, actions]);
  const data =
    owner && loaded?.owner === owner && loaded.key === key ? loaded.data : null;
  async function submit() {
    const scope = lifetime.current;
    if (
      !owner ||
      !scope ||
      scope.signal.aborted ||
      !data?.canReassign ||
      !review ||
      locked ||
      writing.current
    )
      return;
    const isCurrent = () =>
      current.current.owner === owner &&
      current.current.key === key &&
      lifetime.current === scope;
    writing.current = true;
    setBusy(true);
    setMessage("");
    try {
      await reassignInspection(
        branch,
        data,
        Number(inspector),
        reason,
        crypto.randomUUID(),
        { signal: scope.signal, isCurrent },
      );
      if (!scope.signal.aborted && isCurrent())
        setMessage(
          "MSG08: Inspector changed successfully. Refresh to review the current assignment.",
        );
    } catch (error) {
      if (!scope.signal.aborted && isCurrent()) {
        setMessage(
          error instanceof ApiError &&
            [400, 403, 409, 412, 428].includes(error.status)
            ? "Change rejected. Refresh the current assignment before trying again."
            : "Change outcome is unconfirmed. Refresh to inspect the assignment; no automatic retry was sent.",
        );
        if (error instanceof ApiError && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      writing.current = false;
      if (!scope.signal.aborted && isCurrent()) {
        setBusy(false);
        setReview(false);
        setLocked(true);
      }
    }
  }
  if (!owner) return null;
  return (
    <section aria-label="Change inspector">
      <h2>Change inspector</h2>
      {message && <p role="status">{message}</p>}
      {data && (
        <p>
          Current inspector #{data.inspectorId}. State: {data.state}. Checklist
          and deadline remain unchanged.
        </p>
      )}
      {data?.canReassign && !locked && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            setReview(true);
          }}
        >
          <fieldset disabled={busy || review}>
            <label htmlFor={`reassign-inspector-${id}`}>New inspector</label>
            <select
              id={`reassign-inspector-${id}`}
              required
              value={inspector}
              onChange={(e) => setInspector(e.target.value)}
            >
              <option value="">Select inspector</option>
              {data.inspectors.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
            <label>
              Change reason
              <input
                required
                maxLength={500}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
            <button type="submit">Review inspector change</button>
          </fieldset>
          {review && (
            <div>
              <p>
                Change to{" "}
                {data.inspectors.find((p) => p.id === Number(inspector))?.name}:{" "}
                {reason}
              </p>
              <button
                type="button"
                disabled={busy}
                onClick={() => void submit()}
              >
                Confirm inspector change
              </button>
              <button
                type="button"
                disabled={busy}
                onClick={() => setReview(false)}
              >
                Edit change
              </button>
            </div>
          )}
        </form>
      )}
      {data && !data.canReassign && (
        <p>
          No eligible alternative inspector is available with your current
          access.
        </p>
      )}
      <button
        type="button"
        disabled={busy}
        onClick={() => setRefresh((v) => v + 1)}
      >
        Refresh inspector options
      </button>
    </section>
  );
}
