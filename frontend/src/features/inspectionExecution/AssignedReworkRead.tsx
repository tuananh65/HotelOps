import { useEffect, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
import {
  readRework,
  mutateRework,
  type ReworkExecution,
} from "../../api/dev1/reworkExecution";

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
          !["READ_ONLY", "ACCEPT_START"].includes(data.capability) ||
          !["PENDING_ACCEPTANCE", "ACCEPTED", "IN_PROGRESS"].includes(
            data.state,
          )
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
  async function execute(action: "accept" | "start") {
    const scope = lifetime.current;
    const data = visible?.data;
    if (
      !owner ||
      !scope ||
      scope.signal.aborted ||
      writing.current ||
      !data ||
      data.capability !== "ACCEPT_START" ||
      data.state !== (action === "accept" ? "PENDING_ACCEPTANCE" : "ACCEPTED")
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
        { note: action === "accept" ? "Accepted rework" : "Started rework" },
        {
          signal: scope.signal,
          isCurrent: () => currentOwner.current === owner,
        },
      );
      if (!scope.signal.aborted && currentOwner.current === owner)
        setRevision((v) => v + 1);
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
          <ul>
            {visible.data.defects.map((defect) => (
              <li key={defect.id}>
                <p>{defect.description}</p>
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
          {visible.data.capability === "READ_ONLY" && (
            <p>Further actions are currently unavailable.</p>
          )}
        </>
      )}
    </main>
  );
}
