import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { InternalRequestAssignmentPanel } from "./AssignmentPanel";
import { InternalRequestRoutePanel } from "./RoutePanel";

interface DecisionView {
  requestId: number;
  subject: string;
  description: string;
  status: string;
  submittedAt: string;
  deadline: string | null;
  desiredDeadline?: string | null;
  ageSeconds: number;
  version: string;
  canDecide: boolean;
}
class DecisionFailure extends Error {
  constructor(readonly status: number) {
    super("Yêu cầu quyết định thất bại");
  }
}
const version = (v: unknown): v is string =>
  typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
const utc = (v: unknown): v is string =>
  typeof v === "string" &&
  /(?:Z|\+00:00)$/.test(v) &&
  Number.isFinite(Date.parse(v));
function decode(value: unknown, id: number): DecisionView {
  const v = value as DecisionView | null;
  if (
    !v ||
    v.requestId !== id ||
    typeof v.subject !== "string" ||
    typeof v.description !== "string" ||
    typeof v.status !== "string" ||
    !utc(v.submittedAt) ||
    !(v.deadline === null || utc(v.deadline)) ||
    !(
      v.desiredDeadline === undefined ||
      v.desiredDeadline === null ||
      utc(v.desiredDeadline)
    ) ||
    !Number.isFinite(v.ageSeconds) ||
    v.ageSeconds < 0 ||
    !version(v.version) ||
    typeof v.canDecide !== "boolean"
  )
    throw new Error("Phản hồi quyết định không hợp lệ");
  return v;
}
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
  if (!response.ok) throw new DecisionFailure(response.status);
  if (!response.headers.get("content-type")?.includes("application/json"))
    throw new Error("Phản hồi không hợp lệ");
  return response.json();
}

export function InternalRequestDecisionPanel({
  id,
  onChanged,
}: {
  id: number;
  onChanged?: (message: string) => void;
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
    view: DecisionView;
  } | null>(null);
  const [reason, setReason] = useState("");
  const [deadline, setDeadline] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    setResult(null);
    setReason("");
    setDeadline("");
    setMessage("");
    setBusy(false);
    sending.current = false;
    const abort = new AbortController();
    controller.current = abort;
    if (owner)
      void json(`/api/internal-requests/${id}/decision`, abort.signal)
        .then((v) => decode(v, id))
        .then((view) => {
          if (!abort.signal.aborted) setResult({ owner, id, view });
        })
        .catch((e) => {
          if (abort.signal.aborted) return;
          setMessage(
            "Chưa có chi tiết quyết định. Tải lại để kiểm tra quyền và trạng thái.",
          );
          if (e instanceof DecisionFailure && e.status === 401)
            actions.invalidateSession(owner);
        });
    return () => abort.abort();
  }, [id, owner, revision, actions]);
  const view =
    owner && result?.owner === owner && result.id === id ? result.view : null;
  async function decide(accept: boolean) {
    const abort = controller.current;
    if (
      !owner ||
      !view?.canDecide ||
      sending.current ||
      !abort ||
      abort.signal.aborted
    )
      return;
    if ((!accept || deadline) && !reason.trim()) {
      setMessage("Cần nhập lý do khi từ chối hoặc đổi thời hạn.");
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
      const adjustedDeadline =
        accept && deadline ? new Date(`${deadline}Z`).toISOString() : null;
      const raw = (await json(
        `/api/internal-requests/${id}/decision`,
        abort.signal,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": `"${view.version}"`,
          },
          body: JSON.stringify({
            accept,
            reason: reason.trim() || null,
            deadline: adjustedDeadline,
          }),
        },
      )) as {
        requestId?: unknown;
        status?: unknown;
        version?: unknown;
        message?: unknown;
      };
      if (
        raw.requestId !== id ||
        raw.status !== (accept ? "ACCEPTED" : "REJECTED") ||
        !version(raw.version) ||
        raw.message !== (accept ? "MSG08" : "MSG19")
      )
        throw new Error("Chưa xác nhận quyết định");
      if (!abort.signal.aborted) {
        setResult({
          owner,
          id,
          view: {
            ...view,
            status: accept ? "ACCEPTED" : "REJECTED",
            deadline: adjustedDeadline ?? view.deadline,
            version: raw.version,
            canDecide: false,
          },
        });
        setReason("");
        setDeadline("");
        setMessage(
          accept
            ? "MSG08: Đã tiếp nhận yêu cầu."
            : "MSG19: Đã từ chối yêu cầu.",
        );
        onChanged?.(
          accept
            ? "MSG08: Đã tiếp nhận yêu cầu. Phân công người xử lý để bắt đầu."
            : "MSG19: Đã từ chối yêu cầu.",
        );
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        setResult(null);
        setMessage(
          e instanceof DecisionFailure && e.status === 412
            ? "Yêu cầu đã đổi. Tải lại trước khi quyết định."
            : "Chưa xác nhận quyết định. Tải lại trước khi thử lại.",
        );
        if (e instanceof DecisionFailure && e.status === 401)
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
    <section aria-label="Quyết định yêu cầu">
      {message && <p role="status">{message}</p>}
      <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
        Tải lại quyết định
      </button>
      {view && (
        <>
          <h3>{view.subject}</h3>
          <p>{view.description}</p>
          <p>Trạng thái: {displayCode(view.status)}</p>
          <p>
            Đã nộp: {view.submittedAt}; thời gian tồn tại:{" "}
            {Math.floor(view.ageSeconds / 60)} phút.
          </p>
          <p>Thời hạn (UTC): {view.deadline ?? "Chưa có thời hạn"}</p>
          <p>
            Thời hạn người yêu cầu mong muốn (UTC):{" "}
            {view.desiredDeadline ?? "Chưa chỉ định"}
          </p>
          {view.canDecide && (
            <InternalRequestRoutePanel
              id={id}
              onRouted={() => {
                setResult(null);
                (announceWorkflowSuccess(
                  "MSG08: Đã chuyển yêu cầu. Tải lại quyết định để kiểm tra bộ phận mới và quyền hiện tại.",
                  "internalRequests/DecisionPanel.tsx",
                ),
                  setMessage(
                    "MSG08: Đã chuyển yêu cầu. Tải lại quyết định để kiểm tra bộ phận mới và quyền hiện tại.",
                  ));
                onChanged?.(
                  "MSG08: Đã chuyển yêu cầu. Bộ phận và quyền hiện tại đã cập nhật.",
                );
              }}
            />
          )}
          {(view.status === "ACCEPTED" || view.status === "IN_PROGRESS") && (
            <InternalRequestAssignmentPanel
              id={id}
              onAssigned={(version) => {
                setResult((previous) =>
                  previous?.owner === owner && previous.id === id
                    ? {
                        ...previous,
                        view: {
                          ...previous.view,
                          status: "IN_PROGRESS",
                          version,
                          canDecide: false,
                        },
                      }
                    : previous,
                );
                onChanged?.(
                  "MSG08: Đã phân công người xử lý. Yêu cầu đang thực hiện.",
                );
              }}
            />
          )}
          {view.canDecide && (
            <ActionDialog
              scope="internalRequests/DecisionPanel.tsx"
              title="Tiếp nhận hoặc từ chối yêu cầu"
              busy={busy}
              error={message}
            >
              <div aria-busy={busy}>
                <label>
                  Lý do quyết định
                  <input
                    value={reason}
                    maxLength={4000}
                    disabled={busy}
                    onChange={(e) => setReason(e.target.value)}
                  />
                </label>
                <label>
                  Thời hạn đã điều chỉnh (UTC)
                  <input
                    type="datetime-local"
                    value={deadline}
                    disabled={busy}
                    onChange={(e) => setDeadline(e.target.value)}
                  />
                </label>
                <button disabled={busy} onClick={() => void decide(true)}>
                  Tiếp nhận yêu cầu
                </button>
                <button disabled={busy} onClick={() => void decide(false)}>
                  Từ chối yêu cầu
                </button>
              </div>
            </ActionDialog>
          )}
        </>
      )}
    </section>
  );
}
