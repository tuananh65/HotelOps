import { ActionDialog } from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
interface View {
  requestId: number;
  version: string;
  handoverId: number | null;
  state: string | null;
  receiverId: number | null;
  summary: string | null;
  note: string | null;
  canSubmit: boolean;
  canDecide: boolean;
  receivers: { id: number; name: string }[];
  createdAt?: string | null;
  decisionAt?: string | null;
  rejectionReason?: string | null;
}
class Failure extends Error {
  constructor(
    readonly status: number,
    readonly code?: string,
  ) {
    super("Bàn giao chưa khả dụng");
  }
}
const validText = (value: string, required = false) =>
  value.trim().length <= 4000 &&
  (!required || value.trim().length > 0) &&
  !/[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f]/.test(value);
const version = (value: unknown): value is string =>
  typeof value === "string" && /^[A-Za-z0-9+/]{11}=$/.test(value);
async function json(
  path: string,
  signal: AbortSignal,
  init?: RequestInit,
): Promise<unknown> {
  const response = await fetch(path, {
    ...init,
    signal,
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
  });
  if (!response.ok) {
    const problem: unknown = await response.json().catch(() => null);
    const code =
      typeof problem === "object" &&
      problem !== null &&
      "code" in problem &&
      typeof problem.code === "string"
        ? problem.code
        : undefined;
    throw new Failure(response.status, code);
  }
  if (!response.headers.get("content-type")?.includes("json"))
    throw new Error("Phản hồi không hợp lệ");
  return response.json();
}
export function InternalRequestHandoverPanel({
  id,
  handoverId,
  onSaved,
}: {
  id: number;
  handoverId?: number;
  onSaved?: (state: string, receiptId: number) => void;
}) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    !state.pending &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [loaded, setLoaded] = useState<{
    owner: typeof owner;
    id: number;
    receiptId?: number;
    view: View;
  } | null>(null);
  const [committed, setCommitted] = useState<{
    owner: typeof owner;
    id: number;
    receiptId: number;
    state: string;
  } | null>(null);
  const [revision, setRevision] = useState(0);
  const [receiver, setReceiver] = useState("");
  const [summary, setSummary] = useState("");
  const [note, setNote] = useState("");
  const [reason, setReason] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    setLoaded(null);
    setReceiver("");
    setSummary("");
    setNote("");
    setReason("");
    setMessage("");
    setBusy(false);
    sending.current = false;
    const abort = new AbortController();
    controller.current = abort;
    if (owner)
      void json(
        `/api/internal-requests/${id}/handover${handoverId ? `/${handoverId}` : ""}`,
        abort.signal,
      )
        .then((raw) => {
          const v = raw as View;
          if (
            !v ||
            v.requestId !== id ||
            !version(v.version) ||
            typeof v.canSubmit !== "boolean" ||
            typeof v.canDecide !== "boolean" ||
            !Array.isArray(v.receivers) ||
            v.receivers.some(
              (r) =>
                !Number.isSafeInteger(r.id) ||
                r.id <= 0 ||
                typeof r.name !== "string",
            ) ||
            !(
              v.handoverId === null ||
              (Number.isSafeInteger(v.handoverId) && v.handoverId > 0)
            ) ||
            !(
              v.state === null ||
              ["PENDING_ACKNOWLEDGMENT", "ACKNOWLEDGED", "REJECTED"].includes(
                v.state,
              )
            ) ||
            ![v.summary, v.note].every(
              (s) => s === null || typeof s === "string",
            ) ||
            (handoverId !== undefined &&
              (v.handoverId !== handoverId || v.canSubmit)) ||
            ![v.createdAt, v.decisionAt, v.rejectionReason].every(
              (s) => s === undefined || s === null || typeof s === "string",
            ) ||
            (v.canDecide &&
              (!v.handoverId || v.state !== "PENDING_ACKNOWLEDGMENT"))
          )
            throw new Error("Thông tin bàn giao không hợp lệ");
          if (!abort.signal.aborted)
            setLoaded({ owner, id, receiptId: handoverId, view: v });
        })
        .catch((e) => {
          if (!abort.signal.aborted) {
            setMessage(
              "Chưa có bàn giao. Tải lại quyền và trách nhiệm hiện tại.",
            );
            if (e instanceof Failure && e.status === 401)
              actions.invalidateSession(owner!);
          }
        });
    return () => abort.abort();
  }, [id, handoverId, owner, revision, actions]);
  const view =
    owner &&
    loaded?.owner === owner &&
    loaded.id === id &&
    loaded.receiptId === handoverId
      ? loaded.view
      : null;
  const saved =
    owner &&
    committed?.owner === owner &&
    committed.id === id &&
    (handoverId === undefined || committed.receiptId === handoverId)
      ? committed
      : null;
  async function send(action: "submit" | "acknowledge" | "reject") {
    const abort = controller.current;
    if (
      !owner ||
      !view ||
      sending.current ||
      !abort ||
      abort.signal.aborted ||
      (action === "submit" && !view.canSubmit) ||
      (action !== "submit" && !view.canDecide)
    )
      return;
    if (
      (action === "submit" &&
        (!view.receivers.some((r) => r.id === Number(receiver)) ||
          !validText(summary, true) ||
          !validText(note))) ||
      (action !== "submit" && !validText(reason, action === "reject"))
    ) {
      setMessage(
        "Chọn người nhận hợp lệ và nhập tóm tắt / ghi chú hoặc lý do từ chối. Tối đa 4000 ký tự mỗi trường; không có ký tự điều khiển không hỗ trợ.",
      );
      return;
    }
    sending.current = true;
    setBusy(true);
    setMessage("");
    setCommitted(null);
    try {
      const csrf = (await json("/api/auth/csrf", abort.signal)) as {
        csrfToken?: unknown;
        headerName?: unknown;
      };
      if (
        csrf?.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !/^[\x21-\x7e]+$/.test(csrf.csrfToken) ||
        abort.signal.aborted
      )
        throw new Error("Kiểm tra bảo mật thất bại");
      const result = (await json(
        `/api/internal-requests/${id}/handover${action === "submit" ? "" : "/decision"}`,
        abort.signal,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": `"${view.version}"`,
          },
          body: JSON.stringify(
            action === "submit"
              ? {
                  receiverId: Number(receiver),
                  summary: summary.trim(),
                  note: note.trim() || null,
                }
              : {
                  handoverId: view.handoverId,
                  acknowledge: action === "acknowledge",
                  reason: reason.trim() || null,
                },
          ),
        },
      )) as {
        requestId: number;
        handoverId: number;
        state: string;
        version: string;
        message: string;
      };
      if (
        result.requestId !== id ||
        !Number.isSafeInteger(result.handoverId) ||
        result.handoverId <= 0 ||
        !version(result.version) ||
        result.message !== "MSG08" ||
        (action !== "submit" && result.handoverId !== view.handoverId) ||
        result.state !==
          (action === "submit"
            ? "PENDING_ACKNOWLEDGMENT"
            : action === "acknowledge"
              ? "ACKNOWLEDGED"
              : "REJECTED")
      )
        throw new Error("Chưa xác nhận bàn giao");
      if (!abort.signal.aborted) {
        setLoaded(null);
        setSummary("");
        setNote("");
        setReason("");
        setCommitted({
          owner,
          id,
          receiptId: result.handoverId,
          state: result.state,
        });
        onSaved?.(result.state, result.handoverId);
        if (handoverId !== undefined) setRevision((v) => v + 1);
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        if (
          e instanceof Failure &&
          e.status === 400 &&
          e.code === "INVALID_HANDOVER_INPUT"
        ) {
          setMessage(
            "Thông tin bàn giao bị từ chối. Sửa tóm tắt, ghi chú hoặc lý do từ chối rồi thử lại.",
          );
          return;
        }
        setLoaded(null);
        setMessage(
          e instanceof Failure && e.status === 412
            ? "Yêu cầu đã đổi. Tải lại trước khi quyết định."
            : "Chưa xác nhận bàn giao. Tải lại; không tự gửi lại.",
        );
        if (e instanceof Failure && e.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (!abort.signal.aborted) {
        sending.current = false;
        setBusy(false);
      }
    }
  }
  if (!owner) return null;
  return (
    <ActionDialog title="Yêu cầu bàn giao" busy={busy}>
      <section aria-label="Yêu cầu bàn giao" aria-busy={busy}>
        {saved && (
          <p role="status">
            MSG08: Bàn giao {displayCode(saved.state)}.{" "}
            {handoverId === undefined && (
              <Link
                to={`/internal-requests/${id}/handovers/${saved.receiptId}`}
              >
                Mở biên nhận bàn giao của tôi
              </Link>
            )}
          </p>
        )}
        {message && <p role="status">{message}</p>}
        <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
          Tải lại bàn giao
        </button>
        {view?.state && (
          <>
            <p>
              Bàn giao: {displayCode(view.state)}.{" "}
              {view.state === "PENDING_ACKNOWLEDGMENT"
                ? "Người gửi vẫn chịu trách nhiệm đến khi được xác nhận."
                : view.state === "ACKNOWLEDGED"
                  ? "Trách nhiệm đã chuyển khi bàn giao này được xác nhận."
                  : "Bàn giao bị từ chối; chưa chuyển trách nhiệm."}
            </p>
            <p>{view.summary}</p>
            <p>{view.note}</p>
          </>
        )}
        {view?.createdAt && (
          <p>
            Đã nộp: <time>{view.createdAt}</time>
          </p>
        )}
        {view?.decisionAt && (
          <p>
            Quyết định: <time>{view.decisionAt}</time>
          </p>
        )}
        {view?.rejectionReason && <p>Lý do từ chối: {view.rejectionReason}</p>}
        {view?.canSubmit && (
          <>
            {view.receivers.length === 0 && (
              <p role="status">
                Không có người nhận đủ điều kiện và quyền xem bàn giao. Người xử
                lý hiện tại vẫn chịu trách nhiệm.
              </p>
            )}
            <label>
              Người xử lý mới
              <select
                aria-label="Người xử lý mới"
                value={receiver}
                disabled={busy}
                onChange={(e) => setReceiver(e.target.value)}
              >
                <option value="">Chọn</option>
                {view.receivers.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Tổng hợp trạng thái
              <textarea
                maxLength={4000}
                value={summary}
                disabled={busy}
                onChange={(e) => setSummary(e.target.value)}
              />
            </label>
            <label>
              Ghi chú bàn giao
              <textarea
                maxLength={4000}
                value={note}
                disabled={busy}
                onChange={(e) => setNote(e.target.value)}
              />
            </label>
            <button
              data-dialog-action
              disabled={
                busy ||
                !view.receivers.some((r) => r.id === Number(receiver)) ||
                !summary.trim()
              }
              onClick={() => void send("submit")}
            >
              Gửi bàn giao
            </button>
          </>
        )}
        {view?.canDecide && (
          <>
            <label>
              Lý do từ chối bàn giao
              <textarea
                maxLength={4000}
                value={reason}
                disabled={busy}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
            <button
              data-dialog-action
              disabled={busy}
              onClick={() => void send("acknowledge")}
            >
              Xác nhận đã nhận bàn giao
            </button>
            <button
              disabled={busy || !validText(reason, true)}
              onClick={() => void send("reject")}
            >
              Từ chối bàn giao
            </button>
          </>
        )}
      </section>
    </ActionDialog>
  );
}
