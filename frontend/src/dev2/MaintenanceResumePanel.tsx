import { userMessage } from "../i18n/userMessage";
import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function MaintenanceResumePanel({
  data,
  allowStart = false,
  allowHold = false,
  allowResume = true,
  onCommitted,
}: {
  data: Values;
  allowStart?: boolean;
  allowHold?: boolean;
  allowResume?: boolean;
  onCommitted?: (message: string) => void;
}) {
  const { state } = useAuth();
  const session = state.session;
  const current = useRef(session);
  current.current = session;
  const writer = useRef<AbortController | null>(null);
  const sending = useRef(false);
  const [busy, setBusy] = useState(false),
    [message, setMessage] = useState("");
  const [review, setReview] = useState(false),
    [attempted, setAttempted] = useState(false);
  const [reason, setReason] = useState("");
  useEffect(() => () => writer.current?.abort(), [session, data]);
  const id = data.maintenanceWorkId,
    tag = data.executionETag;
  const actor = Number(session?.user.id);
  const start =
    allowStart && data.canStart === true && data.status === "PENDING";
  const hold =
    allowHold && data.canHold === true && data.status === "IN_PROGRESS";
  const resumeAllowed =
    allowResume &&
    data.canResume === true &&
    (data.status === "ON_HOLD" || data.status === "IN_PROGRESS");
  const action = start ? "start" : hold ? "hold" : "resume",
    verb = start ? "Bắt đầu" : hold ? "Tạm dừng" : "Tiếp tục";
  if (
    !session ||
    (!start && !hold && !resumeAllowed) ||
    typeof id !== "number" ||
    !Number.isSafeInteger(id) ||
    id <= 0 ||
    !Number.isSafeInteger(actor) ||
    actor <= 0 ||
    typeof tag !== "string" ||
    !/^"[0-9A-F]{16}"$/.test(tag)
  )
    return null;
  async function resume() {
    if (sending.current || attempted || current.current !== session || !session)
      return;
    if (hold && reason.trim().length < 10) {
      setMessage("Nhập lý do tạm dừng ít nhất 10 ký tự.");
      return;
    }
    sending.current = true;
    setBusy(true);
    setAttempted(true);
    setMessage("");
    const abort = new AbortController();
    writer.current = abort;
    try {
      const csrfResponse = await fetch("/api/auth/csrf", {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
      });
      if (!csrfResponse.ok)
        throw Error(
          "Không xác minh được phiên đăng nhập. Tải lại trước khi thử lại.",
        );
      const csrf: unknown = await csrfResponse.json();
      if (
        !isObject(csrf) ||
        csrf.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !csrf.csrfToken.trim() ||
        /[\r\n]/.test(csrf.csrfToken) ||
        current.current !== session ||
        abort.signal.aborted
      )
        throw Error("Phiên đăng nhập đã thay đổi. Tải lại trước khi thử lại.");
      const response = await fetch(`/api/maintenance-works/${id}/${action}`, {
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
          ...(hold ? { reason: reason.trim() } : {}),
        }),
      });
      if (!response.ok)
        throw Error(
          response.status === 412
            ? "Công việc đã đổi. Tải lại trạng thái hiện tại."
            : response.status === 409
              ? `${verb} chưa được xác nhận. Kiểm tra ngày làm việc và công việc khác đang chạy, rồi tải lại.`
              : `${verb} chưa được xác nhận. Tải lại để kiểm tra quyền và trạng thái công việc.`,
        );
      const result: unknown = await response.json();
      if (
        !isObject(result) ||
        result.maintenanceWorkId !== id ||
        result.status !== (hold ? "ON_HOLD" : "IN_PROGRESS") ||
        result.assigneeId !== actor ||
        typeof result.executionETag !== "string" ||
        !/^"[0-9A-F]{16}"$/.test(result.executionETag) ||
        result.executionETag === tag
      )
        throw Error(
          `${verb} chưa xác minh được phản hồi. Tải lại trước mọi thao tác tiếp theo.`,
        );
      if (current.current === session && !abort.signal.aborted) {
        const notice = `Công việc ${start ? "đã bắt đầu" : hold ? "đã tạm dừng" : "đã tiếp tục"}. ${hold ? "Khoảng làm việc đã kết thúc; thời gian SLA tiếp tục tính." : "Đã mở khoảng thời gian làm việc mới."}`;
        announceWorkflowSuccess(notice, "dev2/MaintenanceResumePanel.tsx");
        setMessage(notice);
        onCommitted?.(notice);
      }
    } catch (error) {
      if (current.current === session && !abort.signal.aborted)
        setMessage(
          error instanceof Error
            ? error.message
            : `${verb} chưa được xác nhận. Tải lại trước khi thử lại.`,
        );
    } finally {
      if (!abort.signal.aborted) {
        sending.current = false;
        setBusy(false);
        setReview(false);
      }
    }
  }
  return (
    <ActionDialog
      scope="dev2/MaintenanceResumePanel.tsx"
      title="Cập nhật trạng thái bảo trì"
      busy={busy}
    >
      <section aria-label={`${verb} Bảo trì`}>
        <p>
          {start
            ? "Tiếp nhận và bắt đầu sẽ ghi nhận thời gian làm việc. Cần ngày làm việc đang hoạt động và không có công việc khác đang chạy."
            : hold
              ? "Tạm dừng sẽ kết thúc khoảng làm việc. Thời gian SLA vẫn tiếp tục tính."
              : "Tiếp tục mở khoảng làm việc mới. Bắt đầu ngày làm việc và dừng công việc khác đang chạy trước."}
        </p>
        {hold && (
          <label>
            Lý do và ghi chú tạm dừng
            <textarea
              value={reason}
              disabled={busy || review || attempted}
              onChange={(e) => setReason(e.target.value)}
            />
          </label>
        )}
        {message && <p role="status">{userMessage(message)}</p>}
        {!review ? (
          <button
            type="button"
            disabled={busy || attempted || (hold && reason.trim().length < 10)}
            onClick={() => setReview(true)}
          >
            {verb} Bảo trì
          </button>
        ) : (
          <>
            <p>{verb} công việc bảo trì được giao này ngay bây giờ?</p>
            {hold && <p>{reason.trim()}</p>}
            <button
              data-dialog-action
              type="button"
              disabled={busy}
              onClick={() => void resume()}
            >
              Xác nhận {action}
            </button>
            <button
              type="button"
              disabled={busy}
              onClick={() => setReview(false)}
            >
              Hủy
            </button>
          </>
        )}
      </section>
    </ActionDialog>
  );
}
