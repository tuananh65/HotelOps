import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function IncidentCancellationPanel({
  data,
  onCommitted,
  onReload,
}: {
  data: Values;
  onCommitted?: (notice: string) => void;
  onReload?: () => void;
}) {
  const { state } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && !state.pending
      ? state.authorityGeneration
      : null;
  const [kind, setKind] = useState("INVALID"),
    [reason, setReason] = useState(""),
    [original, setOriginal] = useState("");
  const [review, setReview] = useState(false),
    [busy, setBusy] = useState(false),
    [attempted, setAttempted] = useState(false);
  const [message, setMessage] = useState<{
    owner: typeof owner;
    text: string;
  } | null>(null);
  const current = useRef(owner),
    sending = useRef(false),
    active = useRef<AbortController | null>(null);
  current.current = owner;
  useEffect(() => () => active.current?.abort(), [owner, data.incidentId]);
  const id = data.incidentId,
    tag = data.triageETag;
  const originalId =
    kind === "DUPLICATE" &&
    /^[1-9][0-9]*$/.test(original) &&
    Number.isSafeInteger(Number(original))
      ? Number(original)
      : null;
  const valid =
    reason.trim().length >= 10 &&
    (kind === "INVALID" || (originalId !== null && originalId !== id));
  async function submit() {
    if (
      !owner ||
      !valid ||
      !review ||
      sending.current ||
      attempted ||
      typeof tag !== "string"
    )
      return;
    sending.current = true;
    setBusy(true);
    setAttempted(true);
    const abort = new AbortController();
    active.current = abort;
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
        current.current !== owner ||
        abort.signal.aborted
      )
        throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      const response = await fetch(`/api/incidents/${id}/cancel-triage`, {
        method: "POST",
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.csrfToken,
          "If-Match": tag,
        },
        body: JSON.stringify({
          kind,
          reason: reason.trim(),
          originalIncidentId: kind === "DUPLICATE" ? originalId : null,
        }),
      });
      if (response.status === 400) {
        const problem: unknown = await response.json();
        if (
          isObject(problem) &&
          problem.title === "Invalid cancellation" &&
          !abort.signal.aborted &&
          current.current === owner
        ) {
          setAttempted(false);
          setMessage({
            owner,
            text:
              typeof problem.detail === "string"
                ? problem.detail
                : "Kiểm tra loại hủy, sự cố gốc và lý do.",
          });
          return;
        }
      }
      if (!response.ok)
        throw Error(
          response.status === 412
            ? "Sự cố đã thay đổi. Tải lại trước khi thử lại."
            : "Chưa xác nhận hủy. Kiểm tra quyền, sự cố gốc và trạng thái chờ tiếp nhận, rồi tải lại.",
        );
      const result: unknown = await response.json();
      if (
        !isObject(result) ||
        result.incidentId !== id ||
        result.status !== "CANCELLED" ||
        typeof result.eventId !== "number" ||
        !Number.isSafeInteger(result.eventId) ||
        result.eventId <= 0
      )
        throw Error("Chưa xác minh được kết quả hủy. Vui lòng tải lại.");
      if (!abort.signal.aborted && current.current === owner) {
        const notice =
          "Đã hủy sự cố, gỡ lệnh khóa hiệu lực của sự cố và tính lại mức sẵn sàng của phòng. Thông báo cho người báo đang chờ gửi theo quyền hiện tại.";
        setMessage({ owner, text: notice });
        announceWorkflowSuccess(notice, "dev2/IncidentCancellationPanel.tsx");
        onCommitted?.(notice);
      }
    } catch (e) {
      if (!abort.signal.aborted && current.current === owner)
        setMessage({
          owner,
          text:
            e instanceof Error
              ? e.message
              : "Chưa xác nhận hủy. Vui lòng tải lại.",
        });
    } finally {
      if (!abort.signal.aborted) {
        sending.current = false;
        setBusy(false);
        setReview(false);
      }
    }
  }
  if (
    !owner ||
    data.canCancelTriage !== true ||
    typeof id !== "number" ||
    !Number.isSafeInteger(id) ||
    id <= 0 ||
    typeof tag !== "string" ||
    !/^"[0-9A-F]{16}"$/.test(tag)
  )
    return null;
  return (
    <ActionDialog
      scope="dev2/IncidentCancellationPanel.tsx"
      title="Hủy sự cố"
      busy={busy}
    >
      <section aria-label="Hủy sự cố khi tiếp nhận">
        <h3>Hủy do trùng lặp / không hợp lệ</h3>
        <fieldset disabled={busy || attempted || review}>
          <label>
            Loại hủy
            <SearchableSelect value={kind} onChange={(e) => setKind(e.target.value)}>
              <option value="INVALID">Không hợp lệ</option>
              <option value="DUPLICATE">Trùng lặp</option>
            </SearchableSelect>
          </label>
          {kind === "DUPLICATE" && (
            <label>
              Mã sự cố gốc
              <input
                inputMode="numeric"
                value={original}
                onChange={(e) => setOriginal(e.target.value)}
              />
            </label>
          )}
          <label>
            Lý do (ít nhất 10 ký tự)
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </label>
        </fieldset>
        {message?.owner === owner && <p role="status">{userMessage(message.text)}</p>}
        {attempted && onReload && (
          <button disabled={busy} onClick={onReload}>
            Tải lại sự cố
          </button>
        )}
        {review ? (
          <>
            <p>
              Hủy sự cố này theo loại {kind.toLowerCase()}
              {originalId ? `, tham chiếu sự cố gốc ${originalId}` : ""}? Chỉ bỏ
              lệnh khóa còn hiệu lực của sự cố này. Lệnh khóa khác và yêu cầu vệ
              sinh vẫn giữ nguyên.
            </p>
            <button
              data-dialog-action
              disabled={busy}
              onClick={() => void submit()}
            >
              Xác nhận hủy sự cố
            </button>
            <button disabled={busy} onClick={() => setReview(false)}>
              Giữ sự cố
            </button>
          </>
        ) : (
          <button
            disabled={!valid || busy || attempted}
            onClick={() => setReview(true)}
          >
            Xem lại hủy
          </button>
        )}
      </section>
    </ActionDialog>
  );
}
