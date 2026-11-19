import { referenceLabel } from "../i18n/referenceLabel";
import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import { useWorkflowAvailability } from "./WorkflowAvailability";
import { staffDate } from "./staffDate";
import { ActionDialog } from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface View {
  requestId: number;
  version: string;
  timingStatus: string;
  canEscalate: boolean;
  hasOpen: boolean;
  createdAt: string;
  deadline: string | null;
  priorityId: number | null;
  priorities: { id: number; name: string }[];
  status: string;
  evaluatedAt: string;
  departmentId: number | null;
  departments: {
    id: number;
    name: string;
    handlers: { id: number; name: string }[];
  }[];
  handlerId: number | null;
  handlers: { id: number; name: string }[];
}
class Failure extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
  ) {
    super("Chuyển cấp xử lý thất bại");
  }
}
const version = (v: unknown): v is string =>
  typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
const validDate = (v: unknown): v is string =>
  typeof v === "string" && /Z$/.test(v) && Number.isFinite(Date.parse(v));
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
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      code?: unknown;
    } | null;
    throw new Failure(
      response.status,
      typeof problem?.code === "string" ? problem.code : "REQUEST_FAILED",
    );
  }
  if (!response.headers.get("content-type")?.includes("application/json"))
    throw new Error("Phản hồi không hợp lệ");
  return response.json();
}

export function InternalRequestEscalationPanel({
  id,
  onSaved,
}: {
  id: number;
  onSaved: () => void;
}) {
  const { state, actions } = useAuth();
  const reportUnavailable = useWorkflowAvailability();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const liveOwner = useRef(owner);
  liveOwner.current = owner;
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState<{
    owner: typeof owner;
    id: number;
    view: View;
  } | null>(null);
  const [reason, setReason] = useState("");
  const [priority, setPriority] = useState("");
  const [deadline, setDeadline] = useState("");
  const [department, setDepartment] = useState("");
  const [handler, setHandler] = useState("");
  const [denied, setDenied] = useState(false);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    setResult(null);
    setReason("");
    setPriority("");
    setDeadline("");
    setHandler("");
    setDepartment("");
    setDenied(false);
    setMessage("");
    setBusy(false);
    sending.current = false;
    const abort = new AbortController();
    controller.current = abort;
    if (owner)
      void json(`/api/internal-requests/${id}/escalation`, abort.signal)
        .then((raw) => {
          const view = raw as View | null;
          if (
            !view ||
            view.requestId !== id ||
            !version(view.version) ||
            !validDate(view.createdAt) ||
            !(view.deadline === null || validDate(view.deadline)) ||
            !(
              view.priorityId === null ||
              (Number.isSafeInteger(view.priorityId) && view.priorityId > 0)
            ) ||
            typeof view.status !== "string" ||
            !validDate(view.evaluatedAt) ||
            !(
              view.departmentId === null ||
              (Number.isSafeInteger(view.departmentId) && view.departmentId > 0)
            ) ||
            !Array.isArray(view.departments) ||
            !view.departments.every(
              (d) =>
                d &&
                Number.isSafeInteger(d.id) &&
                d.id > 0 &&
                d.id !== view.departmentId &&
                typeof d.name === "string" &&
                Array.isArray(d.handlers) &&
                d.handlers.every(
                  (h) =>
                    h &&
                    Number.isSafeInteger(h.id) &&
                    h.id > 0 &&
                    typeof h.name === "string",
                ),
            ) ||
            !(
              view.handlerId === null ||
              (Number.isSafeInteger(view.handlerId) && view.handlerId > 0)
            ) ||
            !Array.isArray(view.handlers) ||
            !view.handlers.every(
              (h) =>
                h &&
                Number.isSafeInteger(h.id) &&
                h.id > 0 &&
                h.id !== view.handlerId &&
                typeof h.name === "string",
            ) ||
            ![
              "AT_RISK",
              "OVERDUE",
              "ON_TIME",
              "INVALID_WINDOW",
              "TERMINAL",
            ].includes(view.timingStatus) ||
            typeof view.canEscalate !== "boolean" ||
            typeof view.hasOpen !== "boolean" ||
            (view.canEscalate &&
              (view.hasOpen ||
                !["AT_RISK", "OVERDUE"].includes(view.timingStatus))) ||
            !Array.isArray(view.priorities) ||
            !view.priorities.every(
              (p) =>
                p &&
                Number.isSafeInteger(p.id) &&
                p.id > 0 &&
                typeof p.name === "string",
            )
          )
            throw new Error("Phản hồi chuyển cấp không hợp lệ");
          if (!abort.signal.aborted && liveOwner.current === owner)
            setResult({ owner, id, view });
        })
        .catch((e) => {
          if (abort.signal.aborted) return;
          if (reportUnavailable(e)) return;
          if (abort.signal.aborted || liveOwner.current !== owner) return;
          if (e instanceof Failure && e.status === 403) setDenied(true);
          else
            setMessage(
              "Không tải được điều kiện chuyển cấp. Tải lại quyền hiện tại.",
            );
          if (e instanceof Failure && e.status === 401)
            actions.invalidateSession(owner);
        });
    return () => abort.abort();
  }, [id, owner, revision, actions, reportUnavailable]);
  const view =
    owner && result?.owner === owner && result.id === id ? result.view : null;
  const targetDepartment = view?.departments.find(
    (d) => d.id === Number(department),
  );
  const handlers = department
    ? (targetDepartment?.handlers ?? [])
    : (view?.handlers ?? []);
  async function submit() {
    const abort = controller.current;
    if (
      !owner ||
      !view?.canEscalate ||
      sending.current ||
      !abort ||
      abort.signal.aborted
    )
      return;
    if (reason.trim().length < 10 || /[\u0000-\u001f\u007f]/.test(reason)) {
      setMessage("Nhập lý do ít nhất 10 ký tự, không chứa ký tự điều khiển.");
      return;
    }
    if (!priority && !deadline && !handler && !department) {
      setMessage(
        "Chọn ưu tiên, thời hạn, bộ phận tiếp nhận hoặc người xử lý mới.",
      );
      return;
    }
    if (department && view.status === "IN_PROGRESS" && !handler) {
      setMessage("Xác nhận người xử lý đủ điều kiện ở bộ phận tiếp nhận mới.");
      return;
    }
    if (handler && !handlers.some((h) => h.id === Number(handler))) {
      setMessage("Tải lại và chọn người xử lý đủ điều kiện.");
      return;
    }
    if (
      department &&
      !view.departments.some((d) => d.id === Number(department))
    ) {
      setMessage("Tải lại và chọn bộ phận tiếp nhận đủ điều kiện.");
      return;
    }
    const parsed = deadline ? new Date(deadline) : null;
    if (
      parsed &&
      (!Number.isFinite(parsed.getTime()) ||
        parsed.getTime() <= Date.parse(view.createdAt))
    ) {
      setMessage("Thời hạn phải sau thời điểm tạo chính thức.");
      return;
    }
    sending.current = true;
    setBusy(true);
    setMessage("");
    const current = () => !abort.signal.aborted && liveOwner.current === owner;
    try {
      const csrf = (await json("/api/auth/csrf", abort.signal)) as {
        csrfToken?: unknown;
      };
      if (!current() || typeof csrf.csrfToken !== "string" || !csrf.csrfToken)
        throw new Error("Kiểm tra bảo mật thất bại");
      const raw = (await json(
        `/api/internal-requests/${id}/escalation`,
        abort.signal,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": `"${view.version}"`,
          },
          body: JSON.stringify({
            reason: reason.trim(),
            priorityId: priority ? Number(priority) : null,
            deadline: parsed?.toISOString() ?? null,
            handlerId: handler ? Number(handler) : null,
            departmentId: department ? Number(department) : null,
          }),
        },
      )) as { requestId?: unknown; eventId?: unknown; version?: unknown };
      if (
        raw.requestId !== id ||
        !Number.isSafeInteger(raw.eventId) ||
        Number(raw.eventId) <= 0 ||
        !version(raw.version)
      )
        throw new Error("Chưa xác nhận chuyển cấp");
      if (current()) {
        setResult(null);
        setReason("");
        setPriority("");
        setDeadline("");
        setHandler("");
        setDepartment("");
        onSaved();
      }
    } catch (e) {
      if (current()) {
        setResult(null);
        setReason("");
        setPriority("");
        setDeadline("");
        setHandler("");
        setDepartment("");
        setMessage(
          e instanceof Failure && e.status === 412
            ? "Yêu cầu đã đổi. Tải lại trước khi chuyển cấp."
            : e instanceof Failure &&
                [
                  "IR_ESCALATION_ALREADY_OPEN",
                  "IR_ESCALATION_NOT_ELIGIBLE",
                ].includes(e.code)
              ? "MSG03: Yêu cầu không đủ điều kiện hoặc đã có chuyển cấp đang mở. Tải lại trạng thái hiện tại."
              : e instanceof Failure && e.code === "IR_HANDOVER_PENDING"
                ? "Có bàn giao đang chờ xác nhận. Hãy xử lý trước khi đổi người chịu trách nhiệm."
                : "Chưa xác nhận chuyển cấp. Tải lại quyền và trạng thái trước khi thử lại.",
        );
        if (e instanceof Failure && e.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (current()) {
        sending.current = false;
        setBusy(false);
      }
    }
  }
  if (!owner || denied || (view && !view.canEscalate && !view.hasOpen))
    return null;
  if (!view)
    return message ? (
      <p role="status">
        {userMessage(message)}{" "}
        <button onClick={() => setRevision((v) => v + 1)}>Thử lại</button>
      </p>
    ) : null;
  return (
    <ActionDialog title="Chuyển cấp xử lý yêu cầu" busy={busy}>
      <section aria-label="Chuyển cấp xử lý yêu cầu" aria-busy={busy}>
        {message && <p role="status">{userMessage(message)}</p>}
        <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
          Tải lại chuyển cấp xử lý
        </button>
        {view && (
          <p>
            Ngày tạo: {staffDate(view.createdAt)} · Hạn xử lý:{" "}
            {staffDate(view.deadline)}. Người xử lý:{" "}
            {view.handlerId === null ? "Chưa phân công" : referenceLabel(view, "handlerId")}
            .{" "}
          </p>
        )}
        {view?.hasOpen && (
          <p role="status">Yêu cầu đã có chuyển cấp đang mở.</p>
        )}
        {view && !view.canEscalate && !view.hasOpen && (
          <p>
            Chuyển cấp cần yêu cầu đang hoạt động, có nguy cơ trễ hoặc đã
            quá hạn và khoảng thời gian hợp lệ.
          </p>
        )}
        {view?.canEscalate && (
          <>
            <label>
              Độ ưu tiên mới
              <SearchableSelect
                aria-label="Độ ưu tiên mới"
                value={priority}
                disabled={busy}
                onChange={(e) => setPriority(e.target.value)}
              >
                <option value="">Giữ độ ưu tiên hiện tại</option>
                {view.priorities.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </SearchableSelect>
            </label>
            <label>
              Thời hạn mới (giờ địa phương)
              <input
                type="datetime-local"
                value={deadline}
                disabled={busy}
                onChange={(e) => setDeadline(e.target.value)}
              />
            </label>
            {handlers.length > 0 && (
              <label>
                Người xử lý mới
                <SearchableSelect
                  aria-label="Người xử lý chuyển cấp"
                  value={handler}
                  disabled={busy}
                  onChange={(e) => setHandler(e.target.value)}
                >
                  <option value="">
                    {department
                      ? "Chọn và xác nhận người tiếp nhận"
                      : "Giữ người xử lý hiện tại"}
                  </option>
                  {handlers.map((h) => (
                    <option key={h.id} value={h.id}>
                      {h.name}
                    </option>
                  ))}
                </SearchableSelect>
              </label>
            )}
            {view.departments.length > 0 && (
              <label>
                Bộ phận tiếp nhận mới
                <SearchableSelect
                  value={department}
                  disabled={busy}
                  onChange={(e) => {
                    setDepartment(e.target.value);
                    setHandler("");
                  }}
                >
                  <option value="">Giữ bộ phận hiện tại</option>
                  {view.departments.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </SearchableSelect>
              </label>
            )}
            {department && view.status === "IN_PROGRESS" && (
              <p>
                Yêu cầu vẫn đang thực hiện. Xác nhận người xử lý hợp lệ ở bộ
                phận đích, có thể giữ người hiện tại nếu đủ điều kiện. Giữ phân
                công và bàn giao trước.
              </p>
            )}
            {department &&
              view.status === "IN_PROGRESS" &&
              handlers.length === 0 && (
                <p role="status">
                  Không có người xử lý đích đủ điều kiện. Không thể chuyển bộ
                  phận.
                </p>
              )}
            {department && view.status === "ACCEPTED" && (
              <p>
                Yêu cầu trở lại chờ trưởng bộ phận đích tiếp nhận. Lần tiếp nhận
                trước giữ trong lịch sử.
              </p>
            )}
            {department && view.status === "PENDING_ACCEPTANCE" && (
              <p>Yêu cầu vẫn chờ trưởng bộ phận đích tiếp nhận.</p>
            )}
            <label>
              Lý do chuyển cấp
              <input
                value={reason}
                maxLength={4000}
                disabled={busy}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
            <button
              disabled={
                busy ||
                (!!department && view.status === "IN_PROGRESS" && !handler)
              }
              onClick={() => void submit()}
            >
              Chuyển cấp xử lý yêu cầu
            </button>
          </>
        )}
      </section>
    </ActionDialog>
  );
}
