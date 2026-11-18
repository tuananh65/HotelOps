import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { TaskProgressError } from "./progress";
import { readTaskSubmission, type TaskSubmission } from "./submission";

export function TaskSubmissionReview({
  branch,
  task,
  owner,
  submission,
  current = false,
  onReady,
}: {
  branch: number;
  task: number;
  owner: AuthorityGeneration;
  submission?: number;
  current?: boolean;
  onReady?: (ready: boolean) => void;
}) {
  const { actions } = useAuth();
  const [result, setResult] = useState<TaskSubmission | null>(null);
  const [error, setError] = useState("");
  const [reload, setReload] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    onReady?.(false);
    setResult(null);
    setError("");
    void readTaskSubmission(branch, task, controller.signal, submission)
      .then((data) => {
        if (!controller.signal.aborted) {
          setResult(data);
          onReady?.(true);
        }
      })
      .catch((reason) => {
        if (controller.signal.aborted) return;
        if (reason instanceof TaskProgressError && reason.status === 401)
          actions.invalidateSession(owner);
        setError(
          reason instanceof TaskProgressError && reason.status === 404
            ? "Chưa có kết quả hoàn tất đã nộp."
            : "Kết quả hoàn tất đã nộp chưa khả dụng hoặc quyền đã đổi.",
        );
      });
    return () => { controller.abort(); onReady?.(false); };
  }, [branch, task, owner, reload, actions, submission, onReady]);
  return (
    <section
      aria-label={
        submission && !current
          ? `Kết quả hoàn tất trước ${submission}`
          : "Kết quả hoàn tất đã nộp"
      }
    >
      <h3>
        {submission && !current ? "Kết quả hoàn tất chu kỳ trước" : "Kết quả hoàn tất đã nộp"}
      </h3>
      <button onClick={() => { onReady?.(false); setReload((n) => n + 1); }}>
        Tải lại kết quả hoàn tất đã nộp
      </button>
      {error && <p>{error}</p>}
      {result && (
        <>
          <p>
            Người nộp {result.submittedBy} lúc{" "}
            <time dateTime={result.submittedAt}>{result.submittedAt}</time>
          </p>
          <h4>Ghi chú kết quả</h4>
          <p style={{ whiteSpace: "pre-wrap" }}>
            {result.resultNote || "Chưa có ghi chú kết quả."}
          </p>
          <h4>Ghi chú bàn giao</h4>
          <p style={{ whiteSpace: "pre-wrap" }}>
            {result.handoverNote || "Chưa có ghi chú bàn giao."}
          </p>
          <ul>
            {result.criteria.map((c) => (
              <li key={c.id}>
                {c.label}: {c.outcome ?? "Chưa ghi nhận"}
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
