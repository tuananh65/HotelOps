import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Technician = {
  userId: number;
  name: string;
  openIncidents: number | null;
  openOccurrences: number | null;
};
type Context = {
  incidentId: number;
  incidentVersion: string;
  workId: number | null;
  workVersion: string | null;
  assigneeId: number | null;
  technicians: Technician[];
  departmentId: number | null;
  departments: { departmentId: number; name: string }[];
};
const positive = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const version = (v: unknown): v is string =>
  typeof v === "string" && /^"[0-9A-F]{16}"$/.test(v);
export function MaintenanceAssignmentPanel({
  incidentId,
  onCommitted,
}: {
  incidentId: number;
  onCommitted?: (notice: string) => void;
}) {
  const { state } = useAuth(),
    owner =
      state.status === "AUTHENTICATED" && !state.pending
        ? state.authorityGeneration
        : null;
  const current = useRef(owner);
  current.current = owner;
  const abort = useRef<AbortController | null>(null),
    sending = useRef(false);
  const [context, setContext] = useState<{
    owner: typeof owner;
    value: Context;
  } | null>(null);
  const [department, setDepartment] = useState("");
  const [selected, setSelected] = useState(""),
    [instructions, setInstructions] = useState(""),
    [end, setEnd] = useState("");
  const [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false),
    [review, setReview] = useState(false),
    [attempted, setAttempted] = useState(false);
  const [saved, setSaved] = useState<number | null>(null);
  useEffect(() => {
    setContext(null);
    setSaved(null);
    setMessage("");
    setReview(false);
    return () => abort.current?.abort();
  }, [owner, incidentId]);
  const data =
    context?.owner === owner && context.value.incidentId === incidentId
      ? context.value
      : null;
  async function load() {
    if (!owner || sending.current) return;
    sending.current = true;
    setBusy(true);
    setMessage("");
    setContext(null);
    const controller = new AbortController();
    abort.current = controller;
    try {
      const response = await fetch(`/api/incidents/${incidentId}/assignment`, {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: controller.signal,
      });
      if (!response.ok)
        throw Error(
          "Chưa thể phân công. Kiểm tra quyền và trạng thái quy trình, rồi tải lại.",
        );
      const r: unknown = await response.json();
      if (
        !isObject(r) ||
        r.incidentId !== incidentId ||
        !version(r.incidentVersion) ||
        !(
          (r.workId === null && r.workVersion === null) ||
          (positive(r.workId) && version(r.workVersion))
        ) ||
        !(r.departmentId === null || positive(r.departmentId)) ||
        !Array.isArray(r.departments) ||
        !r.departments.every(
          (d) =>
            isObject(d) &&
            positive(d.departmentId) &&
            typeof d.name === "string",
        ) ||
        !Array.isArray(r.technicians) ||
        !r.technicians.every(
          (t) =>
            isObject(t) &&
            positive(t.userId) &&
            typeof t.name === "string" &&
            (t.openIncidents === null ||
              (typeof t.openIncidents === "number" &&
                Number.isSafeInteger(t.openIncidents) &&
                t.openIncidents >= 0)) &&
            (t.openOccurrences === null ||
              (typeof t.openOccurrences === "number" &&
                Number.isSafeInteger(t.openOccurrences) &&
                t.openOccurrences >= 0)),
        )
      )
        throw Error("Ngữ cảnh phân công không hợp lệ.");
      if (!controller.signal.aborted && current.current === owner) {
        setContext({ owner, value: r as Context });
        setDepartment(r.departmentId === null ? "" : String(r.departmentId));
        setSelected("");
        setInstructions("");
        setEnd("");
        setReview(false);
        setSaved(null);
        setAttempted(false);
      }
    } catch (e) {
      if (!controller.signal.aborted && current.current === owner)
        setMessage(e instanceof Error ? e.message : "Phân công chưa khả dụng.");
    } finally {
      if (!controller.signal.aborted) {
        sending.current = false;
        setBusy(false);
      }
    }
  }
  const planned =
    end && Number.isFinite(Date.parse(end + "+07:00"))
      ? new Date(end + "+07:00").toISOString()
      : null;
  const chosenDepartment = data?.departments.find(
    (d) => String(d.departmentId) === department,
  );
  const candidate = data?.technicians.find(
    (t) => String(t.userId) === selected,
  );
  async function submit() {
    if (
      !owner ||
      !data ||
      !candidate ||
      !chosenDepartment ||
      !planned ||
      candidate.userId === data.assigneeId ||
      sending.current ||
      attempted
    )
      return;
    sending.current = true;
    setBusy(true);
    setAttempted(true);
    const controller = new AbortController();
    abort.current = controller;
    try {
      const bootstrap = await fetch("/api/auth/csrf", {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: controller.signal,
      });
      if (!bootstrap.ok)
        throw Error("Không xác minh được phiên đăng nhập. Vui lòng tải lại.");
      const csrf: unknown = await bootstrap.json();
      if (
        !isObject(csrf) ||
        csrf.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !/^[\x21-\x7e]+$/.test(csrf.csrfToken) ||
        current.current !== owner ||
        controller.signal.aborted
      )
        throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      const response = await fetch(`/api/incidents/${incidentId}/assignment`, {
        method: "POST",
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: controller.signal,
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.csrfToken,
          "If-Match": data.incidentVersion,
        },
        body: JSON.stringify({
          departmentId: chosenDepartment.departmentId,
          assigneeId: candidate.userId,
          workVersion: data.workVersion,
          instruction: instructions.trim() || null,
          plannedEndAt: planned,
        }),
      });
      if (!response.ok) {
        if (response.status === 400) {
          const problem: unknown = await response.json();
          if (
            isObject(problem) &&
            problem.title === "Invalid assignment" &&
            typeof problem.detail === "string"
          ) {
            if (!controller.signal.aborted && current.current === owner)
              setAttempted(false);
            throw Error(
              problem.detail + "Sửa thông tin phân công và kiểm tra lại.",
            );
          }
        }
        if (
          response.status === 412 &&
          !controller.signal.aborted &&
          current.current === owner
        ) {
          setContext(null);
          setSelected("");
          setAttempted(false);
        }
        throw Error(
          response.status === 412
            ? "Phân công đã thay đổi. Tải danh sách kỹ thuật viên đủ điều kiện trước khi thử lại."
            : "Chưa xác nhận phân công. Tải lại và kiểm tra điều kiện.",
        );
      }
      const result: unknown = await response.json();
      if (
        !isObject(result) ||
        !positive(result.workId) ||
        !positive(result.auditId) ||
        result.workflow !== "ASSIGNED" ||
        (data.workId !== null && data.workId !== result.workId)
      )
        throw Error("Chưa xác minh được phản hồi phân công. Vui lòng tải lại.");
      if (!controller.signal.aborted && current.current === owner) {
        setSaved(result.workId);
        (announceWorkflowSuccess(
          "MSG08: Đã lưu phân công. Thông báo đang chờ gửi đến người đủ điều kiện.",
          "dev2/MaintenanceAssignmentPanel.tsx",
        ),
          setMessage(
            "MSG08: Đã lưu phân công. Thông báo đang chờ gửi đến người đủ điều kiện.",
          ));
        onCommitted?.(
          "MSG08: Đã lưu phân công. Thông báo đang chờ gửi đến người đủ điều kiện.",
        );
      }
    } catch (e) {
      if (!controller.signal.aborted && current.current === owner)
        setMessage(e instanceof Error ? e.message : "Chưa xác nhận phân công.");
    } finally {
      if (!controller.signal.aborted) {
        sending.current = false;
        setBusy(false);
        setReview(false);
      }
    }
  }
  if (!owner || !positive(incidentId)) return null;
  return (
    <ActionDialog
      scope="dev2/MaintenanceAssignmentPanel.tsx"
      title="Phân công bảo trì"
      busy={busy}
    >
      <section aria-label="Phân công bảo trì">
        <h3>Phân công hoặc phân công lại kỹ thuật viên</h3>
        {message && <p role="status">{message}</p>}
        {data && (
          <button disabled={busy} onClick={() => void load()}>
            Tải lại phân công hiện tại và kỹ thuật viên
          </button>
        )}
        {!data ? (
          <button disabled={busy} onClick={() => void load()}>
            Tải kỹ thuật viên đủ điều kiện
          </button>
        ) : (
          <>
            {data.technicians.length === 0 && (
              <p>
                Không có kỹ thuật viên đủ điều kiện. Kiểm tra quyền và người xác
                minh độc lập.
              </p>
            )}
            <label>
              Bộ phận thực hiện
              <select
                aria-label="Bộ phận thực hiện"
                disabled={busy || review || attempted}
                value={department}
                onChange={(e) => setDepartment(e.target.value)}
              >
                <option value="">Chọn bộ phận</option>
                {data.departments.map((d) => (
                  <option key={d.departmentId} value={d.departmentId}>
                    {d.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Kỹ thuật viên
              <select
                aria-label="Kỹ thuật viên"
                disabled={busy || review || attempted}
                value={selected}
                onChange={(e) => setSelected(e.target.value)}
              >
                <option value="">Chọn kỹ thuật viên</option>
                {data.technicians.map((t) => (
                  <option
                    key={t.userId}
                    value={t.userId}
                    disabled={t.userId === data.assigneeId}
                  >
                    {t.name} — {t.openIncidents ?? "N/A"} sự cố đang mở,{" "}
                    {t.openOccurrences ?? "N/A"} Mở các lần thực hiện
                  </option>
                ))}
              </select>
            </label>
            <label>
              Hướng dẫn
              <textarea
                disabled={busy || review || attempted}
                value={instructions}
                onChange={(e) => setInstructions(e.target.value)}
              />
            </label>
            <label>
              Kết thúc dự kiến (giờ Việt Nam, UTC+7)
              <input
                type="datetime-local"
                step="1"
                disabled={busy || review || attempted}
                value={end}
                onChange={(e) => setEnd(e.target.value)}
              />
            </label>
            {review ? (
              <>
                <p>
                  Phân công đến {candidate?.name} trong {chosenDepartment?.name}
                  , Kết thúc dự kiến{" "}
                  {planned &&
                    new Intl.DateTimeFormat("vi-VN", {
                      timeZone: "Asia/Ho_Chi_Minh",
                      dateStyle: "short",
                      timeStyle: "medium",
                    }).format(new Date(planned))}{" "}
                  (UTC+7)? Thời hạn SLA hiện có không đổi.
                </p>
                <button
                  data-dialog-action
                  disabled={busy}
                  onClick={() => void submit()}
                >
                  Xác nhận phân công
                </button>
                <button disabled={busy} onClick={() => setReview(false)}>
                  Hủy
                </button>
              </>
            ) : (
              <button
                disabled={
                  busy ||
                  attempted ||
                  !candidate ||
                  !chosenDepartment ||
                  !planned ||
                  candidate.userId === data.assigneeId
                }
                onClick={() => setReview(true)}
              >
                Xem lại phân công
              </button>
            )}
          </>
        )}
        {saved && (
          <Link to={`/operations/maintenance/${saved}?action=A004A-036`}>
            Mở công việc bảo trì được giao
          </Link>
        )}
      </section>
    </ActionDialog>
  );
}
