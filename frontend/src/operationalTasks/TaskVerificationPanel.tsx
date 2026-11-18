import { displayCode } from "../i18n/vi";
import { useCallback, useEffect, useRef, useState } from "react";
import { TaskSubmissionReview } from "./TaskSubmissionReview";
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
  const [reviewReady, setReviewReady] = useState(false);
  const reviewReadyRef = useRef(false);
  const onReviewReady = useCallback((ready: boolean) => {
    reviewReadyRef.current = ready;
    setReviewReady(ready);
  }, []);
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
        ? "MSG22: Người yêu cầu, người nộp hoặc người thực hiện không được xác minh chu kỳ này."
        : reason instanceof TaskProgressError && reason.status === 412
          ? "Công việc đã đổi. Tải lại và xem xét trước khi quyết định tiếp."
          : "Chưa thể xác minh hoặc chưa xác nhận kết quả. Tải lại trước khi quyết định tiếp.",
    );
    setLocked(true);
  }
  useEffect(() => {
    const controller = new AbortController();
    active.current = controller;
    onReviewReady(false);
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
  }, [branch, task, owner, reload, actions, onReviewReady]);
  async function decide(decision: "ACCEPT" | "RETURN") {
    if (!snapshot?.data.canDecide || !reviewReadyRef.current || locked || busy || saving.current) return;
    if (note.trim().length < (decision === "RETURN" ? 10 : 1)) {
      setError(
        decision === "RETURN"
          ? "Lý do trả lại cần ít nhất 10 ký tự."
          : "Nhập ghi chú xác minh.",
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
        onChanged(result.messageCode + (decision === "ACCEPT" ? ": Đã đóng công việc." : ": Công việc đã trả lại cho người được giao ban đầu. Bắt đầu lại để tiếp tục."));
        return;
      }
      const updated = await readVerification(branch, task, controller.signal);
      if (!controller.signal.aborted) {
        onReviewReady(false);
        setSnapshot(updated);
        setNote("");
        setMessage(
          result.messageCode +
            (decision === "ACCEPT"
              ? ": Đã đóng công việc."
              : ": Công việc đã trả lại cho người được giao ban đầu."),
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
    <section aria-label="Xác minh công việc">
      <h3>Xác minh công việc</h3>
      <button disabled={busy} onClick={() => { onReviewReady(false); setReload((n) => n + 1); }}>
        Tải lại xác minh
      </button>
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
      {snapshot && (
        <>
          <TaskSubmissionReview
            key={`${owner}:${branch}:${task}:${snapshot.data.submissionId}:${snapshot.etag}:${reload}`}
            branch={branch}
            task={task}
            owner={owner}
            submission={snapshot.data.submissionId}
            current
            onReady={onReviewReady}
          />
          <p>Quy trình: {displayCode(snapshot.data.state)}</p>
          <p>
            Thời gian thực hiện thực tế: {snapshot.data.actualEffortSeconds} giây (không tính thời gian tạm dừng)
          </p>
          {snapshot.data.denialCode === "MSG22" && (
            <p>
              MSG22: Người yêu cầu, người nộp hoặc người thực hiện không được xác minh chu kỳ này.
            </p>
          )}
          {snapshot.data.denialCode === "SOURCE_PROVENANCE_REQUIRED" && (
            <p>
              Chưa thể xác minh đến khi kiểm tra được yêu cầu liên quan.
            </p>
          )}
          {snapshot.data.canDecide && (
            <fieldset disabled={busy || locked || !reviewReady}>
              <legend>Xem lại kết quả hoàn tất đã nộp</legend>
              <label>
                Ghi chú xác minh hoặc lý do trả lại
                <textarea
                  maxLength={4000}
                  value={note}
                  onChange={(event) => setNote(event.target.value)}
                />
              </label>
              <button onClick={() => void decide("ACCEPT")}>
                Tiếp nhận hoàn tất
              </button>
              <button onClick={() => void decide("RETURN")}>
                Trả lại để xử lý tiếp
              </button>
            </fieldset>
          )}
          <ol>
            {snapshot.data.decisions.map((d) => (
              <li key={d.submissionId}>
                {d.decision} bởi người dùng {d.verifiedBy} lúc{" "}
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
