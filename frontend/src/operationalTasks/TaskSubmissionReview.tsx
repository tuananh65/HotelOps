import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressError } from "./progress";
import { readTaskSubmission, type TaskSubmission } from "./submission";

export function TaskSubmissionReview({
  branch,
  task,
  owner,
}: {
  branch: number;
  task: number;
  owner: AuthorityGeneration;
}) {
  const { actions } = useAuth();
  const [result, setResult] = useState<TaskSubmission | null>(null);
  const [error, setError] = useState("");
  const [reload, setReload] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setResult(null);
    setError("");
    void readTaskSubmission(branch, task, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setResult(data);
      })
      .catch((reason) => {
        if (controller.signal.aborted) return;
        if (reason instanceof TaskProgressError && reason.status === 401)
          actions.invalidateSession(owner);
        setError(
          reason instanceof TaskProgressError && reason.status === 404
            ? "No submitted completion is available."
            : "Submitted completion is unavailable or access has changed.",
        );
      });
    return () => controller.abort();
  }, [branch, task, owner, reload, actions]);
  return (
    <section aria-label="Submitted completion">
      <h3>Submitted completion</h3>
      <button onClick={() => setReload((n) => n + 1)}>
        Reload submitted completion
      </button>
      {error && <p>{error}</p>}
      {result && (
        <>
          <p>
            Submitted by user {result.submittedBy} at{" "}
            <time dateTime={result.submittedAt}>{result.submittedAt}</time>
          </p>
          <h4>Result note</h4>
          <p style={{ whiteSpace: "pre-wrap" }}>
            {result.resultNote || "No result note."}
          </p>
          <h4>Handover note</h4>
          <p style={{ whiteSpace: "pre-wrap" }}>
            {result.handoverNote || "No handover note."}
          </p>
          <ul>
            {result.criteria.map((c) => (
              <li key={c.id}>
                {c.label}: {c.outcome ?? "Not recorded"}
                {c.note && <p>{c.note}</p>}
              </li>
            ))}
          </ul>
          <ul>
            {result.evidence.map((f) => (
              <li key={f.id}>
                <a href={f.downloadPath} download={f.name}>
                  {f.name}
                </a>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
