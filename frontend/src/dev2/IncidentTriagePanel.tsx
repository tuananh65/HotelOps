import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

type Option = {
  priorityId: number;
  name: string;
  responseDeadlineAt: string;
  resolutionDeadlineAt: string;
  targetVersion: string;
};
const positive = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const instant = (v: unknown) =>
  typeof v === "string" && /Z$/.test(v) && Number.isFinite(Date.parse(v));
const option = (v: unknown): v is Option =>
  isObject(v) &&
  positive(v.priorityId) &&
  typeof v.name === "string" &&
  instant(v.responseDeadlineAt) &&
  instant(v.resolutionDeadlineAt) &&
  typeof v.targetVersion === "string" &&
  /^[0-9A-F]{16}$/.test(v.targetVersion);
const utc = (v: string) =>
  v && Number.isFinite(Date.parse(v + "+07:00"))
    ? new Date(v + "+07:00").toISOString()
    : undefined;
const displayTime = (v: string) =>
  new Intl.DateTimeFormat("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
    dateStyle: "short",
    timeStyle: "medium",
  }).format(new Date(v));
export function IncidentTriagePanel({
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
  const actor = state.session ? Number(state.session.user.id) : 0;
  const [priority, setPriority] = useState("");
  const [plan, setPlan] = useState(""),
    [reason, setReason] = useState("");
  const [response, setResponse] = useState(""),
    [resolution, setResolution] = useState("");
  const [review, setReview] = useState(false),
    [busy, setBusy] = useState(false),
    [attempted, setAttempted] = useState(false);
  const [saved, setSaved] = useState(false),
    [message, setMessage] = useState("");
  const current = useRef(owner),
    active = useRef<AbortController | null>(null),
    sending = useRef(false);
  current.current = owner;
  useEffect(() => () => active.current?.abort(), [owner]);
  const choices = Array.isArray(data.triageOptions)
    ? data.triageOptions.filter(option)
    : [];
  const chosen = choices.find((o) => String(o.priorityId) === priority);
  const id = data.incidentId,
    tag = data.triageETag;
  if (
    owner &&
    positive(id) &&
    data.triageUnavailableReason === "APPROVED_SLA_TARGETS_REQUIRED"
  )
    return (
      <section aria-label="Tiếp nhận sự cố">
        <p role="status">
          Chưa thể tiếp nhận: cần cấu hình thời hạn phản hồi và xử lý SLA đã
          được duyệt cho một mức ưu tiên đang hoạt động.
        </p>
        {onReload && (
          <button onClick={onReload}>Tải lại sự cố và cấu hình SLA</button>
        )}
      </section>
    );
  if (
    owner &&
    positive(id) &&
    data.assignmentWorkflow === "PENDING_TRIAGE" &&
    (!positive(data.categoryId) || !positive(data.severityId))
  )
    return (
      <p role="status">
        MSG03: Cần chọn nhóm và mức độ trước khi tiếp nhận sự cố.{" "}
        <a href="#incident-classification">Mở phần phân loại sự cố</a>.
      </p>
    );
  if (
    !owner ||
    !positive(actor) ||
    !positive(id) ||
    !positive(data.severityId) ||
    data.canTriage !== true ||
    typeof tag !== "string" ||
    !/^"[0-9A-F]{16}"$/.test(tag)
  )
    return null;
  const changed =
    !!chosen &&
    ((response !== "" &&
      Date.parse(utc(response) ?? "") !==
        Date.parse(chosen.responseDeadlineAt)) ||
      (resolution !== "" &&
        Date.parse(utc(resolution) ?? "") !==
          Date.parse(chosen.resolutionDeadlineAt)));
  const valid =
    !!chosen &&
    plan.trim().length > 0 &&
    (!response || !!utc(response)) &&
    (!resolution || !!utc(resolution)) &&
    (!changed || reason.trim().length >= 10);
  async function save() {
    if (!valid || !review || !chosen || sending.current || attempted) return;
    sending.current = true;
    setBusy(true);
    setAttempted(true);
    const session = owner,
      abort = new AbortController();
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
        abort.signal.aborted ||
        current.current !== session
      )
        throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      const res = await fetch(`/api/incidents/${id}/triage`, {
        method: "PATCH",
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
          severityId: data.severityId,
          priorityId: chosen.priorityId,
          expectedSlaTargetVersion: chosen.targetVersion,
          initialActionPlan: plan.trim(),
          responseDeadlineAt: utc(response),
          resolutionDeadlineAt: utc(resolution),
          deadlineChangeReason: changed ? reason.trim() : null,
        }),
      });
      if (res.status === 400) {
        const problem: unknown = await res.json();
        if (
          isObject(problem) &&
          problem.title === "Incident triage validation failed" &&
          !abort.signal.aborted &&
          current.current === session
        ) {
          setAttempted(false);
          setMessage(
            typeof problem.detail === "string"
              ? problem.detail
              : "Kiểm tra độ ưu tiên, thời hạn và kế hoạch xử lý ban đầu.",
          );
          return;
        }
      }
      if (!res.ok)
        throw Error(
          res.status === 412
            ? "Sự cố hoặc cấu hình SLA đã thay đổi. Tải lại trước khi thử lại."
            : "Chưa xác nhận tiếp nhận. Tải lại và kiểm tra quyền, phân loại hiện tại.",
        );
      const result: unknown = await res.json();
      if (
        !isObject(result) ||
        result.incidentId !== id ||
        result.priorityId !== chosen.priorityId ||
        result.severityId !== data.severityId ||
        result.status !== "OPEN" ||
        typeof result.triageETag !== "string" ||
        !/^"[0-9A-F]{16}"$/.test(result.triageETag) ||
        result.triageETag === tag ||
        !isObject(result.sla) ||
        !instant(result.sla.responseDeadlineAt) ||
        !instant(result.sla.resolutionDeadlineAt) ||
        Date.parse(String(result.sla.responseDeadlineAt)) !==
          Date.parse(utc(response) ?? chosen.responseDeadlineAt) ||
        Date.parse(String(result.sla.resolutionDeadlineAt)) !==
          Date.parse(utc(resolution) ?? chosen.resolutionDeadlineAt)
      )
        throw Error(
          "Chưa xác minh được phản hồi tiếp nhận. Tải lại hồ sơ hiện tại.",
        );
      if (!abort.signal.aborted && current.current === session) {
        setSaved(true);
        (announceWorkflowSuccess(
          "Đã lưu thông tin tiếp nhận. Sự cố đang chờ phân công.",
          "dev2/IncidentTriagePanel.tsx",
        ),
          setMessage("Đã lưu thông tin tiếp nhận. Sự cố đang chờ phân công."));
        onCommitted?.("Đã lưu thông tin tiếp nhận. Sự cố đang chờ phân công.");
      }
    } catch (e) {
      if (!abort.signal.aborted && current.current === session)
        setMessage(
          e instanceof Error
            ? e.message
            : "Chưa xác nhận tiếp nhận sự cố. Vui lòng tải lại.",
        );
    } finally {
      if (!abort.signal.aborted) {
        setBusy(false);
        setReview(false);
        sending.current = false;
      }
    }
  }
  return (
    <ActionDialog
      scope="dev2/IncidentTriagePanel.tsx"
      title="Tiếp nhận sự cố"
      busy={busy}
    >
      <section aria-label="Tiếp nhận sự cố">
        <h3>Tiếp nhận sự cố</h3>
        <fieldset disabled={busy || attempted || review}>
          <label>
            Độ ưu tiên
            <select
              aria-label="Độ ưu tiên"
              value={priority}
              onChange={(e) => {
                setPriority(e.target.value);
                setResponse("");
                setResolution("");
                setReason("");
              }}
            >
              <option value="">Chọn độ ưu tiên</option>
              {choices.map((o) => (
                <option key={o.priorityId} value={o.priorityId}>
                  {o.name}
                </option>
              ))}
            </select>
          </label>
          {chosen && (
            <p>
              Hạn phản hồi mặc định: {displayTime(chosen.responseDeadlineAt)};
              hạn xử lý: {displayTime(chosen.resolutionDeadlineAt)} (giờ Việt
              Nam, UTC+7).
            </p>
          )}
          <label>
            Kế hoạch xử lý ban đầu
            <textarea value={plan} onChange={(e) => setPlan(e.target.value)} />
          </label>
          <label>
            Điều chỉnh hạn phản hồi (UTC+7, không bắt buộc)
            <input
              type="datetime-local"
              step="1"
              value={response}
              onChange={(e) => setResponse(e.target.value)}
            />
          </label>
          <label>
            Điều chỉnh hạn xử lý (UTC+7, không bắt buộc)
            <input
              type="datetime-local"
              step="1"
              value={resolution}
              onChange={(e) => setResolution(e.target.value)}
            />
          </label>
          {changed && (
            <label>
              Lý do đổi thời hạn (ít nhất 10 ký tự)
              <textarea
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
          )}
        </fieldset>
        {message && <p role="status">{message}</p>}
        {attempted && !saved && onReload && (
          <button disabled={busy} onClick={onReload}>
            Tải lại sự cố và cấu hình SLA
          </button>
        )}
        {saved ? (
          <Link to={`/operations/incidents/${id}?action=A004A-023`}>
            Tải lại sự cố để phân công
          </Link>
        ) : review ? (
          <>
            <p>Xác nhận độ ưu tiên, thời hạn và kế hoạch xử lý ban đầu?</p>
            <button
              data-dialog-action
              disabled={busy}
              onClick={() => void save()}
            >
              Xác nhận tiếp nhận
            </button>
            <button disabled={busy} onClick={() => setReview(false)}>
              Hủy
            </button>
          </>
        ) : (
          <button
            disabled={!valid || attempted || busy}
            onClick={() => setReview(true)}
          >
            Xem lại thông tin tiếp nhận
          </button>
        )}
      </section>
    </ActionDialog>
  );
}
