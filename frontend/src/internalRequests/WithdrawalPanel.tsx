import { userMessage } from "../i18n/userMessage";
import {
  useWorkflowAvailability,
  workflowProblemCode,
} from "./WorkflowAvailability";
import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface WithdrawalView {
  requestId: number;
  status: string;
  version: string;
  canWithdraw: boolean;
}
class WithdrawalFailure extends Error {
  constructor(
    readonly status: number,
    readonly code?: string,
  ) {
    super("Rút yêu cầu thất bại");
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
  if (!response.ok)
    throw new WithdrawalFailure(
      response.status,
      await workflowProblemCode(response),
    );
  if (!response.headers.get("content-type")?.includes("application/json"))
    throw new Error("Phản hồi không hợp lệ");
  return response.json();
}

export function InternalRequestWithdrawalPanel({
  id,
  onWithdrawn,
}: {
  id: number;
  onWithdrawn: () => void;
}) {
  const { state, actions } = useAuth();
  const reportUnavailable = useWorkflowAvailability();
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
    view: WithdrawalView;
  } | null>(null);
  const [reason, setReason] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    setResult(null);
    setReason("");
    setMessage("");
    setBusy(false);
    sending.current = false;
    const abort = new AbortController();
    controller.current = abort;
    if (owner)
      void json(`/api/internal-requests/${id}/withdrawal`, abort.signal)
        .then((raw) => {
          const view = raw as WithdrawalView | null;
          if (
            !view ||
            view.requestId !== id ||
            typeof view.status !== "string" ||
            !version(view.version) ||
            typeof view.canWithdraw !== "boolean" ||
            (view.canWithdraw && view.status !== "PENDING_ACCEPTANCE")
          )
            throw new Error("Phản hồi rút yêu cầu không hợp lệ");
          if (!abort.signal.aborted) setResult({ owner, id, view });
        })
        .catch((e) => {
          if (abort.signal.aborted) return;
          if (reportUnavailable(e)) return;
          if (e instanceof WithdrawalFailure && [403, 404].includes(e.status))
            return;
          if (abort.signal.aborted) return;
          setMessage(
            "Không tải được điều kiện rút yêu cầu. Tải lại quyền và trạng thái.",
          );
          if (e instanceof WithdrawalFailure && e.status === 401)
            actions.invalidateSession(owner);
        });
    return () => abort.abort();
  }, [id, owner, revision, actions, reportUnavailable]);
  const view =
    owner && result?.owner === owner && result.id === id ? result.view : null;
  async function withdraw() {
    const abort = controller.current;
    if (
      !owner ||
      !view?.canWithdraw ||
      sending.current ||
      !abort ||
      abort.signal.aborted
    )
      return;
    if (!reason.trim()) {
      setMessage("Nhập lý do rút yêu cầu.");
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
        `/api/internal-requests/${id}/withdrawal`,
        abort.signal,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": `"${view.version}"`,
          },
          body: JSON.stringify({ reason: reason.trim() }),
        },
      )) as {
        requestId?: unknown;
        status?: unknown;
        version?: unknown;
        message?: unknown;
      };
      if (
        raw.requestId !== id ||
        raw.status !== "CANCELLED" ||
        !version(raw.version) ||
        raw.message !== "MSG08"
      )
        throw new Error("Chưa xác nhận rút yêu cầu");
      if (!abort.signal.aborted) {
        setResult(null);
        setReason("");
        (announceWorkflowSuccess(
          "MSG08: Đã rút yêu cầu.",
          "internalRequests/WithdrawalPanel.tsx",
        ),
          setMessage("MSG08: Đã rút yêu cầu."));
        onWithdrawn();
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        setResult(null);
        setMessage(
          e instanceof WithdrawalFailure && e.status === 412
            ? "Yêu cầu đã đổi. Tải lại trước khi rút."
            : e instanceof WithdrawalFailure && e.status === 403
              ? "MSG02: Không được rút yêu cầu. Tải lại quyền hiện tại."
              : "Chưa xác nhận rút yêu cầu. Tải lại trước khi thử lại.",
        );
        if (e instanceof WithdrawalFailure && e.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (!abort.signal.aborted) {
        sending.current = false;
        setBusy(false);
      }
    }
  }
  if (!owner || (view && !view.canWithdraw)) return null;
  if (!view)
    return message ? (
      <p role="status">
        {userMessage(message)}{" "}
        <button onClick={() => setRevision((v) => v + 1)}>Thử lại</button>
      </p>
    ) : null;
  return (
    <ActionDialog
      scope="internalRequests/WithdrawalPanel.tsx"
      title="Rút yêu cầu"
      busy={busy}
    >
      <section aria-label="Rút yêu cầu" aria-busy={busy}>
        {message && <p role="status">{userMessage(message)}</p>}
        <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
          Tải lại rút yêu cầu
        </button>
        {view?.canWithdraw && (
          <>
            <p>Có thể rút yêu cầu trước khi được tiếp nhận.</p>
            <label>
              Lý do rút yêu cầu
              <input
                value={reason}
                maxLength={4000}
                disabled={busy}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
            <button disabled={busy} onClick={() => void withdraw()}>
              Rút yêu cầu
            </button>
          </>
        )}
      </section>
    </ActionDialog>
  );
}
