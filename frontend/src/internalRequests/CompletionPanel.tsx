import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface CompletionBlocker {
  kind: string;
  id: number;
  code: string;
}
interface CompletionView {
  requestId: number;
  status: string;
  version: string;
  canComplete: boolean;
  blocker: string | null;
  visibleBlockers?: CompletionBlocker[];
  hasUndisclosedBlockers?: boolean;
}
class CompletionFailure extends Error {
  constructor(readonly status: number) {
    super("Hoàn tất thất bại");
  }
}
const version = (v: unknown): v is string =>
  typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
async function json(
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
  if (!response.ok) throw new CompletionFailure(response.status);
  if (!response.headers.get("content-type")?.includes("application/json"))
    throw new Error("Phản hồi không hợp lệ");
  return response.json();
}

export function InternalRequestCompletionPanel({
  id,
  onCompleted,
}: {
  id: number;
  onCompleted: () => void;
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
    view: CompletionView;
  } | null>(null);
  const [resultNote, setResultNote] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    setResult(null);
    setResultNote("");
    setMessage("");
    setBusy(false);
    sending.current = false;
    const abort = new AbortController();
    controller.current = abort;
    if (owner)
      void json(`/api/internal-requests/${id}/completion`, abort.signal)
        .then((raw) => {
          const view = raw as CompletionView | null;
          if (
            !view ||
            view.requestId !== id ||
            typeof view.status !== "string" ||
            !version(view.version) ||
            typeof view.canComplete !== "boolean" ||
            !(view.blocker === null || typeof view.blocker === "string") ||
            (view.canComplete &&
              (view.status !== "IN_PROGRESS" || view.blocker !== null)) ||
            (view.visibleBlockers !== undefined &&
              (!Array.isArray(view.visibleBlockers) ||
                view.visibleBlockers.some(
                  (b) =>
                    !b ||
                    !["TASK", "CLEANING", "INCIDENT", "MATERIAL"].includes(
                      b.kind,
                    ) ||
                    !Number.isSafeInteger(b.id) ||
                    b.id <= 0 ||
                    typeof b.code !== "string",
                ))) ||
            (view.hasUndisclosedBlockers !== undefined &&
              typeof view.hasUndisclosedBlockers !== "boolean") ||
            (view.canComplete &&
              (view.visibleBlockers?.length || view.hasUndisclosedBlockers))
          )
            throw new Error("Phản hồi hoàn tất không hợp lệ");
          if (!abort.signal.aborted) setResult({ owner, id, view });
        })
        .catch((e) => {
          if (abort.signal.aborted) return;
          setMessage(
            "Không tải được điều kiện hoàn tất. Tải lại để kiểm tra quyền và trạng thái.",
          );
          if (e instanceof CompletionFailure && e.status === 401)
            actions.invalidateSession(owner);
        });
    return () => abort.abort();
  }, [id, owner, revision, actions]);
  const view =
    owner && result?.owner === owner && result.id === id ? result.view : null;
  async function complete() {
    const abort = controller.current;
    if (
      !owner ||
      !view?.canComplete ||
      sending.current ||
      !abort ||
      abort.signal.aborted
    )
      return;
    if (!resultNote.trim()) {
      setMessage("Nhập kết quả hoàn tất.");
      return;
    }
    sending.current = true;
    setBusy(true);
    setMessage("");
    try {
      const csrf = (await json("/api/auth/csrf", abort.signal)) as {
        csrfToken?: unknown;
      };
      if (
        typeof csrf.csrfToken !== "string" ||
        !csrf.csrfToken ||
        abort.signal.aborted
      )
        throw new Error("Kiểm tra bảo mật thất bại");
      const raw = (await json(
        `/api/internal-requests/${id}/completion`,
        abort.signal,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": `"${view.version}"`,
          },
          body: JSON.stringify({ result: resultNote.trim() }),
        },
      )) as {
        requestId?: unknown;
        status?: unknown;
        version?: unknown;
        message?: unknown;
      };
      if (
        raw.requestId !== id ||
        raw.status !== "CLOSED" ||
        !version(raw.version) ||
        raw.message !== "MSG08"
      )
        throw new Error("Chưa xác nhận hoàn tất");
      if (!abort.signal.aborted) {
        setResult(null);
        setResultNote("");
        (announceWorkflowSuccess(
          "MSG08: Đã đóng yêu cầu.",
          "internalRequests/CompletionPanel.tsx",
        ),
          setMessage("MSG08: Đã đóng yêu cầu."));
        onCompleted();
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        setResult(null);
        setMessage(
          e instanceof CompletionFailure && e.status === 412
            ? "Yêu cầu đã đổi. Tải lại trước khi đóng."
            : e instanceof CompletionFailure && e.status === 403
              ? "MSG02: Không được phép hoàn tất. Tải lại quyền hiện tại."
              : "Chưa xác nhận hoàn tất. Tải lại trước khi thử lại.",
        );
        if (e instanceof CompletionFailure && e.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (!abort.signal.aborted) {
        sending.current = false;
        setBusy(false);
      }
    }
  }
  if (!owner || (view && !view.canComplete && !view.blocker)) return null;
  return (
    <ActionDialog
      scope="internalRequests/CompletionPanel.tsx"
      title="Hoàn tất yêu cầu"
      busy={busy}
    >
      <section aria-label="Hoàn tất yêu cầu" aria-busy={busy}>
        {message && <p role="status">{message}</p>}
        {view?.blocker && (
          <p role="status">
            MSG34: Công việc bắt buộc hoặc bàn giao chưa xử lý xong. Kiểm tra
            công việc liên quan bên dưới trước khi đóng.
          </p>
        )}
        {!!view?.visibleBlockers?.length && (
          <ul aria-label="Công việc đang chặn hoàn tất">
            {view.visibleBlockers.map((b) => (
              <li key={`${b.kind}:${b.id}`}>
                {displayCode(b.kind)} #{b.id}:{" "}
                {b.code === "LEGACY_COMPLETION_REQUIREMENT_UNCLASSIFIED"
                  ? "Chưa phân loại điều kiện hoàn tất."
                  : b.code === "MATERIAL_COMPLETION_UNPROVEN"
                    ? "Chưa xác nhận bằng chứng hoàn tất vật tư."
                    : "Công việc bắt buộc chưa đóng."}
              </li>
            ))}
          </ul>
        )}
        {view?.hasUndisclosedBlockers && (
          <p>
            Không thể xem chi tiết các trở ngại khác tại đây. Kiểm tra bàn giao
            đang chờ và nhờ người có quyền xử lý công việc liên quan bị ẩn. Hồ
            sơ bị ẩn vẫn có thể chặn hoàn tất.
          </p>
        )}
        <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
          Tải lại hoàn tất
        </button>
        {view?.canComplete && (
          <>
            <p>
              Xác nhận kết quả sau khi hoàn tất mọi công việc liên quan bắt buộc
              và bàn giao.
            </p>
            <label>
              Kết quả hoàn tất
              <input
                value={resultNote}
                maxLength={4000}
                disabled={busy}
                onChange={(e) => setResultNote(e.target.value)}
              />
            </label>
            <button
              data-dialog-action
              disabled={busy}
              onClick={() => void complete()}
            >
              Hoàn tất yêu cầu
            </button>
          </>
        )}
      </section>
    </ActionDialog>
  );
}
