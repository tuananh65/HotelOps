import { ActionDialog } from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

interface ReopenView {
  requestId: number;
  version: string;
  previousResult: string;
  closedAt: string;
  evaluatedAt: string;
  windowDays: number;
  lastHandlerId: number;
  canReopen: boolean;
  blocker: string | null;
}
class ReopenFailure extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
  ) {
    super(code);
  }
}
const version = (v: unknown): v is string =>
  typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
const time = (v: unknown): v is string =>
  typeof v === "string" && Number.isFinite(Date.parse(v));
const displayTime = (v: string) =>
  new Date(v).toLocaleString("vi-VN", { timeZone: "Asia/Bangkok" }) +
  " (UTC+7)";
async function read(
  path: string,
  signal: AbortSignal,
  init?: RequestInit,
): Promise<unknown> {
  const response = await fetch(path, {
    ...init,
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
    signal,
  });
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      code?: unknown;
    } | null;
    throw new ReopenFailure(
      response.status,
      typeof problem?.code === "string" ? problem.code : "REQUEST_FAILED",
    );
  }
  if (!response.headers.get("content-type")?.includes("application/json"))
    throw new Error("Phản hồi không hợp lệ");
  const value = (await response.json()) as { version?: unknown };
  if (
    path !== "/api/auth/csrf" &&
    (!version(value?.version) ||
      response.headers.get("etag") !== `"${value.version}"`)
  )
    throw new Error("Chưa xác nhận phiên bản");
  return value;
}

export function InternalRequestReopenPanel({
  id,
  onReopened,
}: {
  id: number;
  onReopened: () => void;
}) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState<{
    owner: typeof owner;
    id: number;
    view: ReopenView;
  } | null>(null);
  const [reason, setReason] = useState("");
  const [message, setMessage] = useState("");
  const [hidden, setHidden] = useState(true);
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    setResult(null);
    setReason("");
    setMessage("");
    setBusy(false);
    setHidden(true);
    sending.current = false;
    const abort = new AbortController();
    controller.current = abort;
    if (owner)
      void read(`/api/internal-requests/${id}/reopen`, abort.signal)
        .then((raw) => {
          const view = raw as ReopenView | null;
          if (
            !view ||
            view.requestId !== id ||
            !version(view.version) ||
            typeof view.previousResult !== "string" ||
            !time(view.closedAt) ||
            !time(view.evaluatedAt) ||
            !Number.isSafeInteger(view.windowDays) ||
            view.windowDays <= 0 ||
            !Number.isSafeInteger(view.lastHandlerId) ||
            view.lastHandlerId <= 0 ||
            typeof view.canReopen !== "boolean" ||
            !(view.blocker === null || typeof view.blocker === "string") ||
            (view.canReopen && view.blocker !== null)
          )
            throw new Error("Phản hồi mở lại không hợp lệ");
          if (!abort.signal.aborted) {
            setResult({ owner, id, view });
            setHidden(false);
          }
        })
        .catch((e) => {
          if (abort.signal.aborted) return;
          if (e instanceof ReopenFailure && e.code === "IR_NOT_CLOSED") return;
          setHidden(false);
          setMessage(
            "Không tải được điều kiện mở lại. Tải lại trạng thái và quyền hiện tại.",
          );
          if (e instanceof ReopenFailure && e.status === 401)
            actions.invalidateSession(owner);
        });
    return () => abort.abort();
  }, [id, owner, revision, actions]);
  const view =
    owner && result?.owner === owner && result.id === id ? result.view : null;
  async function reopen() {
    const abort = controller.current;
    if (
      !owner ||
      !view?.canReopen ||
      sending.current ||
      !abort ||
      abort.signal.aborted
    )
      return;
    const note = reason.trim();
    if (
      note.length < 10 ||
      note.length > 4000 ||
      /[\u0000-\u001f\u007f-\u009f]/.test(note)
    ) {
      setMessage("Nhập lý do 10–4000 ký tự, không chứa ký tự điều khiển.");
      return;
    }
    sending.current = true;
    setBusy(true);
    setMessage("");
    try {
      const csrf = (await read("/api/auth/csrf", abort.signal)) as {
        csrfToken?: unknown;
      };
      if (
        typeof csrf.csrfToken !== "string" ||
        !csrf.csrfToken ||
        abort.signal.aborted
      )
        throw new Error("Kiểm tra bảo mật thất bại");
      const response = (await read(
        `/api/internal-requests/${id}/reopen`,
        abort.signal,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": `"${view.version}"`,
          },
          body: JSON.stringify({ reason: note }),
        },
      )) as {
        requestId?: unknown;
        eventId?: number;
        status?: unknown;
        version?: unknown;
        messageCode?: unknown;
      };
      if (
        response.requestId !== id ||
        response.status !== "IN_PROGRESS" ||
        !version(response.version) ||
        !Number.isSafeInteger(response.eventId) ||
        response.eventId! <= 0 ||
        response.messageCode !== "MSG08"
      )
        throw new Error("Chưa xác nhận mở lại");
      if (!abort.signal.aborted) {
        setResult(null);
        setReason("");
        setHidden(true);
        onReopened();
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        setResult(null);
        setMessage(
          e instanceof ReopenFailure && e.code === "IR_REOPEN_WINDOW_EXPIRED"
            ? "MSG03: Đã hết thời hạn mở lại. Tạo yêu cầu mới."
            : e instanceof ReopenFailure && e.status === 412
              ? "Yêu cầu đã đổi. Tải lại trước khi mở lại."
              : e instanceof ReopenFailure && e.status === 403
                ? "MSG02: Quyền hiện tại không cho phép mở lại."
                : "Chưa xác nhận mở lại. Tải lại trước khi thử lại.",
        );
        if (e instanceof ReopenFailure && e.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (!abort.signal.aborted) {
        sending.current = false;
        setBusy(false);
      }
    }
  }
  if (!owner || hidden) return null;
  return (
    <ActionDialog title="Yêu cầu mở lại" busy={busy}>
      <section aria-label="Yêu cầu mở lại" aria-busy={busy}>
        <h3>Mở lại yêu cầu đã đóng</h3>
        {message && <p role="status">{message}</p>}
        {view && (
          <>
            <p>Kết quả trước: {view.previousResult}</p>
            <p>
              Đã đóng: {displayTime(view.closedAt)}. Reopen window:{" "}
              {view.windowDays} ngày sau khi đóng.
            </p>
            <p>
              Đánh giá từ máy chủ: {displayTime(view.evaluatedAt)}. Last
              handler: #{view.lastHandlerId}.
            </p>
            {view.blocker && (
              <p role="status">
                {view.blocker === "IR_REOPEN_WINDOW_EXPIRED"
                  ? "MSG03: Đã hết thời hạn mở lại. Tạo yêu cầu mới."
                  : view.blocker === "IR_LAST_HANDLER_INELIGIBLE"
                    ? "Người xử lý cuối cùng không còn đủ điều kiện. Không thể mở lại."
                    : view.blocker === "IR_REOPEN_REQUIRED" ||
                        view.blocker === "IR_AUTHORITY_CHANGED"
                      ? "MSG02: Cần quyền mở lại hiện tại."
                      : "Dữ liệu mở lại chưa khả dụng. Liên hệ quản trị viên."}
              </p>
            )}
            {view.canReopen && (
              <>
                <p>
                  Mở lại đưa yêu cầu về đang thực hiện với người xử lý cuối cùng
                  và giữ nguyên kết quả, bàn giao trước đó.
                </p>
                <label>
                  Lý do mở lại
                  <input
                    value={reason}
                    maxLength={4000}
                    disabled={busy}
                    onChange={(e) => setReason(e.target.value)}
                  />
                </label>
                <button
                  data-dialog-action
                  disabled={busy || reason.trim().length < 10}
                  onClick={() => void reopen()}
                >
                  Xác nhận mở lại
                </button>
              </>
            )}
          </>
        )}
        <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
          Tải lại điều kiện mở lại
        </button>{" "}
        <Link to="/internal-requests/new">Tạo yêu cầu mới</Link>
      </section>
    </ActionDialog>
  );
}
