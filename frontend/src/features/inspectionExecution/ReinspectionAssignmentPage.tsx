import { useEffect, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
import { inspectionAssignmentRejection } from "../../api/dev1/inspectionReassignment";
import { InspectionReassignmentForm } from "./InspectionReassignmentForm";
import {
  assignReinspection,
  readReinspectionOptions,
  type ReinspectionOptions,
} from "../../api/dev1/reinspectionAssignment";

export function ReinspectionAssignmentPage() {
  const { state } = useAuth();
  const { id = "" } = useParams();
  const [params] = useSearchParams();
  const branch = params.get("branchId") ?? "";
  const identity = useRef(state.authorityGeneration);
  const revision = useRef(0);
  if (identity.current !== state.authorityGeneration) {
    identity.current = state.authorityGeneration;
    ++revision.current;
  }
  return (
    <Assignment
      key={`${branch}:${id}:${revision.current}`}
      branch={branch}
      id={id}
    />
  );
}

function Assignment({ branch, id }: { branch: string; id: string }) {
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
  const [result, setResult] = useState<{
    owner: typeof owner;
    key: string;
    data?: ReinspectionOptions;
    error?: string;
  } | null>(null);
  const [inspector, setInspector] = useState("");
  const [due, setDue] = useState("");
  const [reason, setReason] = useState("");
  const [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false);
  const [locked, setLocked] = useState(false);
  const [message, setMessage] = useState("");
  const [saved, setSaved] = useState<{
    owner: typeof owner;
    source: string;
    id: number;
  } | null>(null);
  const sourceKey = `${branch}:${id}`;
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    lifetime.current = abort;
    setResult(null);
    setReview(false);
    setInspector("");
    void readReinspectionOptions(branch, id, abort.signal)
      .then((data) => {
        if (!abort.signal.aborted) setResult({ owner, key, data });
      })
      .catch((error) => {
        if (!abort.signal.aborted) {
          setResult({
            owner,
            key,
            error: "This rework is unavailable or you no longer have access.",
          });
          if (error instanceof ApiError && error.status === 401)
            actions.invalidateSession(owner);
        }
      });
    return () => {
      abort.abort();
      if (lifetime.current === abort) lifetime.current = null;
    };
  }, [owner, key, branch, id, actions]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  const data = visible?.data;
  const confirmed =
    owner && saved?.owner === owner && saved.source === sourceKey
      ? saved
      : null;
  async function submit() {
    const scope = lifetime.current;
    if (
      !owner ||
      !scope ||
      scope.signal.aborted ||
      !data?.canAssign ||
      !review ||
      writing.current ||
      locked ||
      confirmed
    )
      return;
    const inspectorId = Number(inspector),
      deadline = new Date(due);
    if (
      !data.inspectors.some((p) => p.id === inspectorId) ||
      !Number.isFinite(deadline.getTime()) ||
      deadline.getTime() <= Date.now() ||
      !reason.trim() ||
      reason.trim().length > 500
    ) {
      setMessage(
        "Select an eligible inspector, a future due time and a reason.",
      );
      return;
    }
    writing.current = true;
    setBusy(true);
    setMessage("");
    const isCurrent = () =>
      current.current.owner === owner &&
      current.current.key === key &&
      lifetime.current === scope;
    try {
      const assigned = await assignReinspection(
        branch,
        id,
        data.reworkETag,
        crypto.randomUUID(),
        { inspectorId, dueAt: deadline.toISOString(), reason: reason.trim() },
        { signal: scope.signal, isCurrent },
      );
      if (!scope.signal.aborted && isCurrent()) {
        setSaved({ owner, source: sourceKey, id: assigned.id });
        setLocked(true);
        setReview(false);
      }
    } catch (error) {
      if (!scope.signal.aborted && isCurrent()) {
        setLocked(true);
        setReview(false);
        setMessage(
          inspectionAssignmentRejection(error) ??
            (error instanceof ApiError &&
            [400, 403, 409, 412, 428].includes(error.status)
              ? "Assignment was rejected. Refresh to review the current inspection; no automatic retry was sent."
              : "Assignment outcome is unconfirmed. Refresh to inspect the current state; do not submit another assignment."),
        );
        if (error instanceof ApiError && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      writing.current = false;
      if (!scope.signal.aborted && isCurrent()) setBusy(false);
    }
  }
  if (!owner) return <p role="status">Checking session…</p>;
  return (
    <main>
      <h1>Assign reinspection</h1>
      <Link to="/notifications">Notifications</Link>
      {!visible && <p role="status">Loading submitted rework…</p>}
      {visible?.error && <p role="alert">{visible.error}</p>}
      {confirmed && (
        <p role="status">
          <span>
            Inspection assigned successfully. Inspection #{confirmed.id}.
          </span>
          <Link to={`/reinspections/${branch}/${confirmed.id}/execution`}>
            Open reinspection
          </Link>
        </p>
      )}
      {message && <p role="alert">{message}</p>}
      {data && (
        <>
          <h2>Room {data.roomNumber}</h2>
          <p>
            Rework #{data.reworkId} · Cleaning cycle #{data.cleaningTaskId}
          </p>
          <p>
            {data.checklistName} · Version {data.checklistVersionNumber}
          </p>
          {data.existingInspectionId !== null && (
            <p role="status">
              <span>
                MSG03: An inspection is already assigned: #
                {data.existingInspectionId}.
              </span>
              <Link
                to={`/reinspections/${branch}/${data.existingInspectionId}/execution`}
              >
                Open reinspection
              </Link>
            </p>
          )}
          {data.existingInspectionId !== null && (
            <InspectionReassignmentForm
              key={`${key}:${data.existingInspectionId}`}
              branch={branch}
              id={data.existingInspectionId}
              reinspection
            />
          )}
          {!data.canAssign && data.existingInspectionId === null && (
            <p role="status">
              {data.unavailableReason === "CHECKLIST_UNAVAILABLE"
                ? "MSG03: The original checklist version is unavailable for assignment. Publish the eligible checklist before assignment; the original defects cannot be remapped to another version."
                : data.unavailableReason === "NO_ELIGIBLE_INSPECTOR"
                  ? "No eligible inspector is available for this cleaning cycle."
                  : "Assignment is unavailable with your current access."}
            </p>
          )}
          {data.canAssign && !confirmed && !locked && (
            <form
              onSubmit={(event) => {
                event.preventDefault();
                setMessage("");
                setReview(true);
              }}
            >
              <fieldset disabled={busy || review}>
                <label htmlFor="reinspection-inspector">Inspector</label>
                <select
                  id="reinspection-inspector"
                  required
                  value={inspector}
                  onChange={(event) => setInspector(event.target.value)}
                >
                  <option value="">Select inspector</option>
                  {data.inspectors.map((person) => (
                    <option key={person.id} value={person.id}>
                      {person.name}
                    </option>
                  ))}
                </select>
                <label>
                  Due time
                  <input
                    required
                    type="datetime-local"
                    value={due}
                    onChange={(event) => setDue(event.target.value)}
                  />
                </label>
                <label>
                  Assignment reason
                  <textarea
                    required
                    maxLength={500}
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                  />
                </label>
                <button type="submit">Review assignment</button>
              </fieldset>
              {review && (
                <section aria-label="Review reinspection assignment">
                  <p>
                    Assign to{" "}
                    {
                      data.inspectors.find(
                        (person) => person.id === Number(inspector),
                      )?.name
                    }{" "}
                    by {due.replace("T", " ")}.
                  </p>
                  <p>{reason}</p>
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => void submit()}
                  >
                    Confirm assignment
                  </button>
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => setReview(false)}
                  >
                    Edit assignment
                  </button>
                </section>
              )}
            </form>
          )}
        </>
      )}
      {busy && <p role="status">Assignment pending…</p>}
      <button
        type="button"
        disabled={busy}
        onClick={() => setRefresh((value) => value + 1)}
      >
        Refresh reinspection
      </button>
    </main>
  );
}
