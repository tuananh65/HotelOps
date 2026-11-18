import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function MaintenanceReturnPanel({
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
  const [reason, setReason] = useState(""),
    [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false),
    [attempted, setAttempted] = useState(false),
    [message, setMessage] = useState("");
  const sending = useRef(false),
    writer = useRef<AbortController | null>(null);
  useEffect(() => () => writer.current?.abort(), [session, data]);
  const id = data.maintenanceWorkId,
    owner = data.assigneeId,
    tag = data.executionETag,
    actor = Number(session?.user.id);
  if (
    !session ||
    session !== bound ||
    data.canReturn !== true ||
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
      reason.trim().length < 10 ||
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
      const response = await fetch(`/api/maintenance-works/${id}/return`, {
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
        body: JSON.stringify({ actorUserId: actor, reason: reason.trim() }),
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
            : "Chưa xác nhận trả lại. Tải lại và kiểm tra quyền người xác minh cùng người được giao hiện tại.",
        );
      const result: unknown = await response.json();
      if (
        !isObject(result) ||
        result.maintenanceWorkId !== id ||
        result.assigneeId !== owner ||
        result.status !== "IN_PROGRESS" ||
        typeof result.executionETag !== "string" ||
        !/^"[0-9A-F]{16}"$/.test(result.executionETag) ||
        result.executionETag === tag
      )
        throw Error(
          "Chưa xác minh được phản hồi trả lại. Tải lại trạng thái hiện tại.",
        );
      if (!abort.signal.aborted && current.current === session) {
        const notice =
          "Đã trả lại kết quả. Người thực hiện cần Tiếp tục để mở khoảng làm việc mới.";
        announceWorkflowSuccess(notice, "dev2/MaintenanceReturnPanel.tsx");
        setMessage(notice);
        onCommitted?.(notice);
      }
    } catch (error) {
      if (!abort.signal.aborted && current.current === session)
        setMessage(
          error instanceof Error
            ? error.message
            : "Chưa xác nhận hoàn trả. Vui lòng tải lại.",
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
      scope="dev2/MaintenanceReturnPanel.tsx"
      title="Trả lại kết quả bảo trì"
      busy={busy}
    >
      <section aria-label="Trả lại kết quả bảo trì">
        <h3>Trả kết quả để xử lý tiếp</h3>
        <p>
          Xem kết quả đã nộp bên dưới. Trả lại giữ nguyên khóa phòng và không
          bắt đầu tính thời gian thực hiện.
        </p>
        {message && <p role="status">{message}</p>}
        <label>
          Lý do trả lại
          <textarea
            disabled={busy || attempted || review}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
          />
        </label>
        {review ? (
          <>
            <p>Trả kết quả này cho người đang được giao?</p>
            <p>{reason.trim()}</p>
            <button
              data-dialog-action
              disabled={busy}
              onClick={() => void submit()}
            >
              Xác nhận hoàn trả
            </button>
            <button disabled={busy} onClick={() => setReview(false)}>
              Hủy
            </button>
          </>
        ) : (
          <button
            disabled={reason.trim().length < 10 || busy || attempted}
            onClick={() => setReview(true)}
          >
            Xem lại hoàn trả
          </button>
        )}
      </section>
    </ActionDialog>
  );
}
