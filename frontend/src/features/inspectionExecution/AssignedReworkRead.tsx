import { useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
import {
  readRework,
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
  const [result, setResult] = useState<{
    owner: typeof owner;
    key: string;
    data?: ReworkExecution;
    error?: string;
  } | null>(null);
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    setResult(null);
    void readRework(branch, id, abort.signal)
      .then((data) => {
        if (
          data.capability !== "READ_ONLY" ||
          data.state !== "PENDING_ACCEPTANCE"
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
    return () => abort.abort();
  }, [owner, key, branch, id, actions]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  return (
    <main aria-label="Assigned cleaning rework">
      <h1>Assigned cleaning rework</h1>
      <Link to="/notifications">Notifications</Link>
      <button disabled={!owner} onClick={() => setRevision((v) => v + 1)}>
        Refresh rework
      </button>
      {owner && !visible && <p role="status">Loading rework...</p>}
      {visible?.error && <p role="alert">{visible.error}</p>}
      {visible?.data && (
        <>
          <p>Rework #{visible.data.id} is pending acceptance.</p>
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
          <p>Accept and Start are currently unavailable.</p>
        </>
      )}
    </main>
  );
}
