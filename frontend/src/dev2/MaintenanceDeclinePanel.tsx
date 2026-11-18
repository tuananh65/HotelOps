import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function MaintenanceDeclinePanel({ data, onCommitted }: { data: Values; onCommitted?: (message: string) => void }) {
  const { state } = useAuth(), session = state.session;
  const [bound] = useState(session), current = useRef(session); current.current = session;
  const [reason, setReason] = useState(""), [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false), [attempted, setAttempted] = useState(false), [message, setMessage] = useState("");
  const sending = useRef(false), writer = useRef<AbortController | null>(null);
  useEffect(() => () => writer.current?.abort(), [session, data]);
  const id = data.maintenanceWorkId, owner = data.assigneeId, tag = data.executionETag, actor = Number(session?.user.id);
  if (!session || session !== bound || data.canDecline !== true || data.status !== "PENDING"
    || typeof id !== "number" || !Number.isSafeInteger(id) || id <= 0
    || typeof owner !== "number" || !Number.isSafeInteger(owner) || owner <= 0 || owner !== actor
    || !Number.isSafeInteger(actor) || actor <= 0 || typeof tag !== "string" || !/^"[0-9A-F]{16}"$/.test(tag)) return null;
  async function submit() {
    if (reason.trim().length < 10 || reason.trim().length > 2000 || sending.current || attempted || current.current !== session) return;
    sending.current = true; setBusy(true); setAttempted(true); setMessage("");
    const abort = new AbortController(); writer.current = abort;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!bootstrap.ok) throw Error("Không xác minh được phiên đăng nhập. Tải lại trước khi thử lại.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string"
        || !/^[\x21-\x7e]+$/.test(csrf.csrfToken) || abort.signal.aborted || current.current !== session)
        throw Error("Phiên đăng nhập đã thay đổi. Tải lại trước khi thử lại.");
      const response = await fetch(`/api/maintenance-works/${id}/decline`, {
        method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": tag as string },
        body: JSON.stringify({ reason: reason.trim() })
      });
      if (!response.ok) throw Error(response.status === 412 ? "Công việc đã đổi. Tải lại trước khi từ chối tiếp."
        : "Chưa xác nhận từ chối. Tải lại và kiểm tra phân công.");
      const result: unknown = await response.json();
      if (!isObject(result) || result.maintenanceWorkId !== id || result.workflow !== "PENDING_ASSIGNMENT"
        || typeof result.auditId !== "number" || !Number.isSafeInteger(result.auditId) || result.auditId <= 0
        || typeof result.declinedAt !== "string" || Number.isNaN(Date.parse(result.declinedAt)))
        throw Error("Chưa xác minh được phản hồi từ chối. Tải lại trạng thái hiện tại.");
      if (!abort.signal.aborted && current.current === session) {
        const notice = "Đã từ chối nhận việc. Công việc đang chờ phân công.";
        setMessage(notice);
        setReason("");
        onCommitted?.(notice);
      }
    } catch (error) {
      if (!abort.signal.aborted && current.current === session) setMessage(error instanceof Error ? error.message : "Chưa xác nhận từ chối nhận việc. Vui lòng tải lại.");
    } finally { if (!abort.signal.aborted) { setBusy(false); setReview(false); sending.current = false; } }
  }
  return <section aria-label="Từ chối nhận phân công bảo trì">
    <h3>Từ chối nhận phân công</h3>
    <p>Từ chối sẽ bỏ phân công hiện tại của bạn. Thời hạn SLA và lệnh khóa phòng giữ nguyên.</p>
    {message && <p role="status">{message}</p>}
    <label>Lý do từ chối nhận việc<textarea maxLength={2000} disabled={busy || attempted || review} value={reason} onChange={e => setReason(e.target.value)} /></label>
    {review ? <><p>Từ chối phân công này và thông báo cho trưởng bảo trì?</p><p>{reason.trim()}</p>
      <button disabled={busy} onClick={() => void submit()}>Xác nhận từ chối nhận việc</button><button disabled={busy} onClick={() => setReview(false)}>Hủy</button></>
      : <button disabled={reason.trim().length < 10 || busy || attempted} onClick={() => setReview(true)}>Xem lại từ chối nhận việc</button>}
  </section>;
}
