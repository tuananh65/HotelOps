import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function MaintenanceVerifyPanel({
  data,
  onCommitted,
}: {
  data: Values;
  onCommitted?: (message: string) => void;
}) {
  const { state } = useAuth(),
    session = state.session;
  const [bound] = useState(session),
    current = useRef(session);
  current.current = session;
  const [note, setNote] = useState(""),
    [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false),
    [attempted, setAttempted] = useState(false),
    [message, setMessage] = useState("");
  const sending = useRef(false),
    writer = useRef<AbortController | null>(null);
  useEffect(() => () => writer.current?.abort(), [session, data]);
  const submitted = isObject(data.submittedReview)
    ? data.submittedReview.submissionAuditId
    : null;
  const id = data.maintenanceWorkId,
    owner = data.assigneeId,
    tag = data.executionETag,
    actor = Number(session?.user.id);
  if (
    typeof submitted !== "number" ||
    !Number.isSafeInteger(submitted) ||
    submitted <= 0 ||
    !session ||
    session !== bound ||
    data.canVerify !== true ||
    data.status !== "COMPLETED" ||
    typeof id !== "number" ||
    !Number.isSafeInteger(id) ||
    id <= 0 ||
    typeof owner !== "number" ||
    !Number.isSafeInteger(owner) ||
    owner <= 0 ||
    owner === actor ||
    !Number.isSafeInteger(actor) ||
    actor <= 0 ||
    typeof tag !== "string" ||
    !/^"[0-9A-F]{16}"$/.test(tag)
  )
    return null;
  async function submit() {
    if (
      note.trim().length < 10 ||
      sending.current ||
      attempted ||
      current.current !== session
    )
      return;
    sending.current = true;
    setBusy(true);
    setAttempted(true);
    setMessage("");
    const abort = new AbortController();
    writer.current = abort;
    try {
      const bootstrap = await fetch("/api/auth/csrf", {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
      });
      if (!bootstrap.ok)
        throw Error(
          "Không xác minh được phiên đăng nhập. Tải lại trước khi thử lại.",
        );
      const csrf: unknown = await bootstrap.json();
      if (
        !isObject(csrf) ||
        csrf.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !/^[\x21-\x7e]+$/.test(csrf.csrfToken) ||
        abort.signal.aborted ||
        current.current !== session
      )
        throw Error("Phiên đăng nhập đã thay đổi. Tải lại trước khi thử lại.");
      const response = await fetch(`/api/maintenance-works/${id}/verify`, {
        method: "POST",
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.csrfToken,
          "If-Match": tag as string,
        },
        body: JSON.stringify({
          actorUserId: actor,
          note: note.trim(),
          submissionAuditId: submitted,
        }),
      });
      if (response.status === 400) {
        const problem: unknown = await response.json();
        if (
          isObject(problem) &&
          problem.title === "Maintenance validation failed" &&
          typeof problem.detail === "string"
        ) {
          if (!abort.signal.aborted && current.current === session)
            setAttempted(false);
          throw Error(
            problem.detail +
              "Sửa kết quả hoặc tải lại công việc trước khi xem xét lại.",
          );
        }
      }
      if (!response.ok)
        throw Error(
          response.status === 412
            ? "Công việc đã đổi. Tải lại trước khi xét duyệt tiếp."
            : "Chưa xác nhận đóng. Tải lại và kiểm tra quyền xác minh, bằng chứng và sử dụng vật tư.",
        );
      const result: unknown = await response.json();
      if (
        !isObject(result) ||
        result.maintenanceWorkId !== id ||
        result.assigneeId !== owner ||
        result.status !== "CLOSED" ||
        typeof result.executionETag !== "string" ||
        !/^"[0-9A-F]{16}"$/.test(result.executionETag) ||
        result.executionETag === tag
      )
        throw Error(
          "Chưa xác minh được phản hồi đóng. Tải lại trạng thái hiện tại.",
        );
      if (!abort.signal.aborted && current.current === session) {
        const notice =
          "Đã đóng công việc và sự cố. Mức sẵn sàng phòng được tính lại; bỏ khóa sự cố không đồng nghĩa phòng sẵn sàng.";
        announceWorkflowSuccess(notice, "dev2/MaintenanceVerifyPanel.tsx");
        setMessage(notice);
        onCommitted?.(notice);
      }
    } catch (error) {
      if (!abort.signal.aborted && current.current === session)
        setMessage(
          error instanceof Error
            ? error.message
            : "Chưa xác nhận đóng hồ sơ. Vui lòng tải lại.",
        );
    } finally {
      if (!abort.signal.aborted) {
        setBusy(false);
        setReview(false);
        sending.current = false;
      }
    }
  }
  return (
    <ActionDialog
      scope="dev2/MaintenanceVerifyPanel.tsx"
      title="Chấp nhận và đóng hồ sơ"
      busy={busy}
    >
      <section aria-label="Xác minh kết quả bảo trì">
        <h3>Chấp nhận và đóng</h3>
        <p>
          Kiểm tra kết quả, bằng chứng và vật tư trước khi đóng. Lệnh khóa phòng
          khác vẫn hiệu lực.
        </p>
        {message && <p role="status">{message}</p>}
        <label>
          Ghi chú xác minh
          <textarea
            disabled={busy || attempted || review}
            value={note}
            onChange={(e) => setNote(e.target.value)}
          />
        </label>
        {review ? (
          <>
            <p>
              Chấp nhận kết quả đã nộp và đóng công việc bảo trì cùng sự cố?
            </p>
            <p>{note.trim()}</p>
            <button
              data-dialog-action
              disabled={busy}
              onClick={() => void submit()}
            >
              Xác nhận đóng hồ sơ
            </button>
            <button disabled={busy} onClick={() => setReview(false)}>
              Hủy
            </button>
          </>
        ) : (
          <button
            disabled={note.trim().length < 10 || busy || attempted}
            onClick={() => setReview(true)}
          >
            Xem lại đóng hồ sơ
          </button>
        )}
      </section>
    </ActionDialog>
  );
}
