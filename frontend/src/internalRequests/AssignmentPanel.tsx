import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface AssignmentView {
  requestId: number;
  status: string;
  version: string;
  handlerId: number | null;
  handlers: { userId: number; name: string; openRequests: number }[];
}
class Failure extends Error {
  constructor(readonly status: number) {
    super("Phân công chưa khả dụng");
  }
}
const version = (v: unknown): v is string =>
  typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
async function json(
  path: string,
  signal: AbortSignal,
  init?: RequestInit,
): Promise<unknown> {
  const r = await fetch(path, {
    ...init,
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
    signal,
  });
  if (!r.ok) throw new Failure(r.status);
  if (!r.headers.get("content-type")?.includes("application/json"))
    throw new Error("Phản hồi không hợp lệ");
  return r.json();
}
export function InternalRequestAssignmentPanel({
  id,
  onAssigned,
}: {
  id: number;
  onAssigned?: (version: string) => void;
}) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [result, setResult] = useState<{
    owner: typeof owner;
    id: number;
    view: AssignmentView;
  } | null>(null);
  const [revision, setRevision] = useState(0);
  const [handler, setHandler] = useState("");
  const [note, setNote] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    controller.current = abort;
    setResult(null);
    setHandler("");
    setNote("");
    setMessage("");
    setBusy(false);
    sending.current = false;
    if (owner)
      void json(`/api/internal-requests/${id}/assignment`, abort.signal)
        .then((raw) => {
          const v = raw as AssignmentView;
          if (
            !v ||
            v.requestId !== id ||
            typeof v.status !== "string" ||
            !version(v.version) ||
            !(
              v.handlerId === null ||
              (Number.isSafeInteger(v.handlerId) && v.handlerId > 0)
            ) ||
            !Array.isArray(v.handlers) ||
            v.handlers.some(
              (h) =>
                !Number.isSafeInteger(h.userId) ||
                h.userId <= 0 ||
                typeof h.name !== "string" ||
                !Number.isSafeInteger(h.openRequests) ||
                h.openRequests < 0,
            )
          )
            throw new Error("Phản hồi phân công không hợp lệ");
          if (!abort.signal.aborted) setResult({ owner, id, view: v });
        })
        .catch((e) => {
          if (abort.signal.aborted) return;
          setMessage("Chưa thể phân công người xử lý. Tải lại quyền hiện tại.");
          if (e instanceof Failure && e.status === 401)
            actions.invalidateSession(owner!);
        });
    return () => abort.abort();
  }, [id, owner, revision, actions]);
  const view =
    owner && result?.owner === owner && result.id === id ? result.view : null;
  async function assign() {
    const abort = controller.current;
    if (
      !owner ||
      !view ||
      !abort ||
      abort.signal.aborted ||
      sending.current ||
      !["ACCEPTED", "IN_PROGRESS"].includes(view.status)
    )
      return;
    const selected = view.handlers.find((h) => h.userId === Number(handler));
    if (!selected || selected.userId === view.handlerId) {
      setMessage("Chọn người xử lý khác đủ điều kiện.");
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
        `/api/internal-requests/${id}/assignment`,
        abort.signal,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": `"${view.version}"`,
          },
          body: JSON.stringify({
            handlerId: selected.userId,
            note: note.trim() || null,
          }),
        },
      )) as { requestId?: unknown; status?: unknown; version?: unknown };
      if (
        raw.requestId !== id ||
        raw.status !== "IN_PROGRESS" ||
        !version(raw.version)
      )
        throw new Error("Chưa xác nhận phân công");
      if (!abort.signal.aborted) {
        setResult(null);
        setHandler("");
        setNote("");
        (announceWorkflowSuccess(
          "MSG08: Đã phân công người xử lý. Yêu cầu đang thực hiện. Tải lại người xử lý trước khi thay đổi tiếp.",
          "internalRequests/AssignmentPanel.tsx",
        ),
          setMessage(
            "MSG08: Đã phân công người xử lý. Yêu cầu đang thực hiện. Tải lại người xử lý trước khi thay đổi tiếp.",
          ));
        onAssigned?.(raw.version);
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        setResult(null);
        setMessage(
          e instanceof Failure && e.status === 412
            ? "Yêu cầu đã đổi. Tải lại người xử lý trước khi phân công."
            : "Chưa xác nhận phân công. Tải lại trước khi thử lại.",
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
    <ActionDialog
      scope="internalRequests/AssignmentPanel.tsx"
      title="Phân công người xử lý yêu cầu"
      busy={busy}
    >
      <section aria-label="Phân công người xử lý yêu cầu">
        <h3>Phân công người xử lý</h3>
        {message && <p role="status">{message}</p>}
        <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
          Tải lại người xử lý
        </button>
        {view &&
          (["ACCEPTED", "IN_PROGRESS"].includes(view.status) ? (
            <div aria-busy={busy}>
              <p>
                Người xử lý hiện tại:{" "}
                {view.handlers.find((h) => h.userId === view.handlerId)?.name ??
                  (view.handlerId ? "Chưa khả dụng" : "Chưa phân công")}
              </p>
              <label>
                Người xử lý yêu cầu
                <select
                  aria-label="Người xử lý yêu cầu"
                  disabled={busy}
                  value={handler}
                  onChange={(e) => setHandler(e.target.value)}
                >
                  <option value="">Chọn người xử lý</option>
                  {view.handlers.map((h) => (
                    <option
                      key={h.userId}
                      value={h.userId}
                      disabled={h.userId === view.handlerId}
                    >
                      {h.name} ({displayCode(h.openRequests)} yêu cầu đang mở)
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Ghi chú phân công
                <input
                  disabled={busy}
                  maxLength={4000}
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                />
              </label>
              <button disabled={busy || !handler} onClick={() => void assign()}>
                Phân công người xử lý yêu cầu
              </button>
            </div>
          ) : (
            <p>Tiếp nhận yêu cầu trước khi phân công người xử lý.</p>
          ))}
      </section>
    </ActionDialog>
  );
}
