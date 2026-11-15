import { useEffect, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
import {
  readRework,
  mutateRework,
  type ReworkExecution,
  type ReworkCommand,
} from "../../api/dev1/reworkExecution";
import { ReworkSubmissionForm } from "./ReworkSubmissionForm";

export function AssignedReworkRead() {
  const { state, actions } = useAuth();
  const { id = "" } = useParams();
  const [params] = useSearchParams();
  const branch = params.get("branchId") || "";
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    state.session &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [revision, setRevision] = useState(0);
  const key = `${branch}:${id}:${revision}`;
  const currentOwner = useRef(owner);
  currentOwner.current = owner;
  const lifetime = useRef<AbortController | null>(null);
  const writing = useRef(false);
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState<{
    owner: typeof owner;
    source: string;
    submitted: boolean;
  } | null>(null);
  const [result, setResult] = useState<{
    owner: typeof owner;
    key: string;
    data?: ReworkExecution;
    error?: string;
  } | null>(null);
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    lifetime.current = abort;
    setBusy(false);
    setResult(null);
    void readRework(branch, id, abort.signal)
      .then((data) => {
        if (
          !["READ_ONLY", "ACCEPT_START", "SUBMIT_REWORK"].includes(
            data.capability,
          ) ||
          ![
            "PENDING_ACCEPTANCE",
            "ACCEPTED",
            "IN_PROGRESS",
            "PENDING_REINSPECTION",
            "COMPLETED",
            "FAILED",
          ].includes(data.state)
        )
          throw new ApiError(502, "UNSUPPORTED_REWORK_READ_STATE");
        if (!abort.signal.aborted) setResult({ owner, key, data });
      })
      .catch((error) => {
        if (!abort.signal.aborted)
          setResult({
            owner,
            key,
            error:
              "Rework is unavailable or you no longer have access. Refresh to try again.",
          });
        if (
          !abort.signal.aborted &&
          error instanceof ApiError &&
          error.status === 401
        )
          actions.invalidateSession(owner);
      });
    return () => {
      abort.abort();
      if (lifetime.current === abort) lifetime.current = null;
    };
  }, [owner, key, branch, id, actions]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  async function execute(
    action: "accept" | "start" | "complete",
    command?: ReworkCommand,
  ) {
    const scope = lifetime.current;
    const data = visible?.data;
    if (
      !owner ||
      !scope ||
      scope.signal.aborted ||
      writing.current ||
      !data ||
      currentOwner.current !== owner ||
      data.capability !==
        (action === "complete" ? "SUBMIT_REWORK" : "ACCEPT_START") ||
      data.state !==
        (action === "accept"
          ? "PENDING_ACCEPTANCE"
          : action === "start"
            ? "ACCEPTED"
            : "IN_PROGRESS") ||
      (action === "complete" && !command)
    )
      return;
    writing.current = true;
    setBusy(true);
    try {
      await mutateRework(
        branch,
        id,
        action,
        data.sourceETag,
        crypto.randomUUID(),
        command || {
          note: action === "accept" ? "Accepted rework" : "Started rework",
        },
        {
          signal: scope.signal,
          isCurrent: () => currentOwner.current === owner,
        },
      );
      if (!scope.signal.aborted && currentOwner.current === owner) {
        setSaved({
          owner,
          source: `${branch}:${id}`,
          submitted: action === "complete",
        });
        setRevision((v) => v + 1);
      }
    } catch (error) {
      if (!scope.signal.aborted && currentOwner.current === owner) {
        setResult({
          owner,
          key,
          error:
            error instanceof ApiError && error.code === "TIME_WORKDAY_REQUIRED"
              ? "Start a working workday, then refresh rework before starting."
              : error instanceof ApiError &&
                  error.code === "TIME_INTERVAL_ALREADY_OPEN"
                ? "Finish or pause your current work before starting this rework. Refresh to retry."
                : "The change could not be confirmed. Refresh rework before trying again.",
        });
        if (error instanceof ApiError && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      writing.current = false;
      if (currentOwner.current === owner) setBusy(false);
    }
  }
  return (
    <main aria-label="Assigned cleaning rework">
      <h1>Assigned cleaning rework</h1>
      <Link to="/notifications">Notifications</Link>
      <button
        disabled={!owner || busy}
        onClick={() => setRevision((v) => v + 1)}
      >
        Refresh rework
      </button>
      {owner && !visible && <p role="status">Loading rework...</p>}
      {visible?.error && <p role="alert">{visible.error}</p>}
      {visible?.data &&
        saved?.owner === owner &&
        saved.source === `${branch}:${id}` && (
          <p role="status">
            {saved.submitted
              ? "Rework has been submitted and is waiting for reinspection."
              : "Rework has been saved successfully."}
          </p>
        )}
      {busy && <p role="status">Saving rework...</p>}
      {visible?.data && (
        <>
          <p>
            Rework #{visible.data.id}:{" "}
            {visible.data.state.replaceAll("_", " ").toLowerCase()}.
          </p>
          <p>
            Room {visible.data.roomId}; failed inspection{" "}
            {visible.data.inspectionId}.
          </p>
          <p>Due: {visible.data.dueAtUtc || "Not specified"}</p>
          {visible.data.conclusion && (
            <section aria-label="Reinspection result">
              <p>Reinspection #{visible.data.conclusion.reinspectionId}: {visible.data.conclusion.state.toLowerCase()}.</p>
              <p>Current room readiness: {visible.data.conclusion.effectiveReadiness}.</p>
              <p>Readiness at conclusion: {visible.data.conclusion.readinessAtConclusion}.</p>
              <p>This rework is closed. Room availability is checked against current room conditions.</p>
            </section>
          )}
          <ul>
            {visible.data.defects.map((defect) => (
              <li key={defect.id}>
                <p>{defect.description}</p>
                {defect.remediation && <p>Remediation: {defect.remediation}</p>}
                {defect.remediationAttachmentIds.map((file) => (
                  <a
                    key={`remediation-${file}`}
                    href={`/api/branches/${visible.data!.branchId}/reworks/${visible.data!.id}/evidence/${file}`}
                  >
                    View remediation photo {file}
                  </a>
                ))}
                {defect.inspectionAttachmentIds.map((file) => (
                  <a
                    key={file}
                    href={`/api/branches/${visible.data!.branchId}/reworks/${visible.data!.id}/evidence/${file}`}
                  >
                    View defect photo {file}
                  </a>
                ))}
              </li>
            ))}
          </ul>
          {visible.data.capability === "ACCEPT_START" &&
            visible.data.state === "PENDING_ACCEPTANCE" && (
              <button disabled={busy} onClick={() => void execute("accept")}>
                Accept rework
              </button>
            )}
          {visible.data.capability === "ACCEPT_START" &&
            visible.data.state === "ACCEPTED" && (
              <button disabled={busy} onClick={() => void execute("start")}>
                Start rework
              </button>
            )}
          {visible.data.capability === "READ_ONLY" && !visible.data.conclusion && (
            <p>
              {visible.data.state === "PENDING_REINSPECTION"
                ? "Waiting for reinspection."
                : "Further actions are currently unavailable."}
            </p>
          )}
          {visible.data.capability === "SUBMIT_REWORK" &&
            visible.data.state === "IN_PROGRESS" && (
              <ReworkSubmissionForm
                key={visible.data.sourceETag}
                data={visible.data}
                busy={busy}
                submit={(command) => execute("complete", command)}
              />
            )}
        </>
      )}
    </main>
  );
}
